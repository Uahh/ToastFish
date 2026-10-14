import Foundation

/// Wraps any random source so tests can inject a seeded one.
public struct StudyRandom: RandomNumberGenerator {
    private let nextValue: () -> UInt64

    public init(_ next: @escaping () -> UInt64) { nextValue = next }
    public static var system: StudyRandom {
        var generator = SystemRandomNumberGenerator()
        return StudyRandom { generator.next() }
    }
    public mutating func next() -> UInt64 { nextValue() }
}

public enum StudyOutcome: Equatable, Sendable {
    case finished
    case cancelled
    case noWords
}

/// English study flows ported from PushWords.cs: RecitationSM2, Recitation and PushWaitAllQuestions.
/// Runs on the main actor; the only suspension points are notifications and pauses.
@MainActor
public final class EnglishStudySession {
    public struct Options: Sendable {
        public var book: String
        public var number: Int
        public var engType: Int
        public var autoPlay: Bool

        public init(book: String, number: Int, engType: Int, autoPlay: Bool) {
            self.book = book
            self.number = number
            self.engType = engType
            self.autoPlay = autoPlay
        }
    }

    private let database: Database
    private let presenter: StudyPresenter
    private let options: Options
    private let now: () -> Date
    private var random: StudyRandom

    /// Every word studied in the last run, for the session log.
    public private(set) var studiedWords: [Word] = []

    public init(database: Database, presenter: StudyPresenter, options: Options,
                now: @escaping () -> Date = { Date() }, random: StudyRandom = .system) {
        self.database = database
        self.presenter = presenter
        self.options = options
        self.now = now
        self.random = random
    }

    // MARK: SM2 recitation (开始！ on an English book)

    public func runSM2() async throws -> StudyOutcome {
        studiedWords = []
        let allWords = try database.allWords(book: options.book)
        var newCards = try database.generateRandomNewCardList(book: options.book, max: options.number)
        var reviewCards = try database.overdueReviewedCards(book: options.book, max: 2 * options.number, now: now())
        if newCards.isEmpty && reviewCards.isEmpty {
            presenter.show("好..好像词库里没有单词了，您就是摸鱼之王！")
            return .noWords
        }

        var learning: [Card] = []
        var finished: [Card] = []

        func sortLearning() { learning.sort { $0.dateLearningDue < $1.dateLearningDue } }

        /// Shows one card and applies the answer. False when cancelled.
        func study(_ card: Card) async -> Card? {
            var card = card
            guard let score = await grade(card, new: newCards.count, learning: learning.count,
                                          reviewed: reviewCards.count) else { return nil }
            card.updateCard(score: score, now: now, random: { Double.random(in: 0..<1, using: &self.random) })
            return card
        }

        func place(_ card: Card) {
            if card.status == .reviewed {
                finished.append(card)
            } else {
                learning.append(card)
                sortLearning()
            }
        }

        // New cards first, then overdue reviews; after each, revisit learning cards that are due.
        for queueIsNew in [true, false] {
            while let card = queueIsNew ? newCards.first : reviewCards.first {
                guard let updated = await study(card) else { return try cancel(saving: finished) }
                if queueIsNew { newCards.removeFirst() } else { reviewCards.removeFirst() }
                place(updated)
                while let due = learning.first, due.isDue(now: now()) {
                    guard let updated = await study(due) else { return try cancel(saving: finished) }
                    learning.removeFirst()
                    place(updated)
                }
            }
        }
        // Remaining learning cards are drilled in due order without waiting.
        while let card = learning.first {
            guard let updated = await study(card) else { return try cancel(saving: finished) }
            learning.removeFirst()
            place(updated)
        }

        try database.updateCards(finished)
        try database.updateTableCount(book: options.book)

        let byOverdue = finished.sorted { $0.percentOverdue(now: now()) > $1.percentOverdue(now: now()) }
        studiedWords = byOverdue.map(\.word)
        let fuzzy = byOverdue.filter { $0.lastScore != Parameters.easy }.map(\.word)
        if !fuzzy.isEmpty {
            presenter.show("背完了！接下来开始测验记忆模糊的单词！")
            await presenter.pause(seconds: 3)
            guard await runQuiz(fuzzy, allWords: allWords) else { return .cancelled }
        }
        presenter.show("结束了！恭喜！")
        return .finished
    }

    /// Finished cards are saved even when the user restarts mid-session (C# lost them).
    private func cancel(saving finished: [Card]) throws -> StudyOutcome {
        try database.updateCards(finished)
        try database.updateTableCount(book: options.book)
        studiedWords = finished.map(\.word)
        return .cancelled
    }

    private func grade(_ card: Card, new: Int, learning: Int, reviewed: Int) async -> Double? {
        if options.autoPlay { presenter.pronounce(card.word, engType: options.engType) }
        let prompt = StudyContent.sm2Prompt(card, engType: options.engType, new: new, learning: learning, reviewed: reviewed)
        while true {
            guard let answer = await presenter.ask(prompt) else { return nil }
            switch answer {
            case "again": return Parameters.again
            case "hard": return Parameters.hard
            case "good": return Parameters.good
            case "easy": return Parameters.easy
            default:
                // "voice" and a click on the notification body both replay the word (C# maps both to 0).
                presenter.pronounce(card.word, engType: options.engType)
            }
        }
    }

    // MARK: Plain recitation (imported word lists)

    /// Ported from PushWords.Recitation. `persist` marks remembered words learned in `options.book`.
    public func runRecitation(_ words: [Word], allWords: [Word], persist: Bool) async throws -> StudyOutcome {
        studiedWords = words
        if words.isEmpty {
            if persist { presenter.show("好..好像词库里没有单词了，您就是摸鱼之王！") }
            return .noWords
        }
        var queue = words
        while let current = queue.first {
            if options.autoPlay { presenter.pronounce(current, engType: options.engType) }
            var remembered: Bool?
            while remembered == nil {
                guard let answer = await presenter.ask(StudyContent.recitationPrompt(current, engType: options.engType)) else {
                    return .cancelled
                }
                switch answer {
                case "succeed": remembered = true
                case "voice": presenter.pronounce(current, engType: options.engType)
                default: remembered = false
                }
            }
            queue.removeFirst()
            if remembered == true {
                if persist {
                    try database.markWordLearned(book: options.book, wordRank: current.wordRank)
                    try database.updateTableCount(book: options.book)
                }
            } else if queue.count == 1 {
                queue.append(current)
            } else if queue.count >= 2 {
                // Re-insert somewhere after the next word, as C# did.
                queue.insert(current, at: Int.random(in: 1...(queue.count - 1), using: &random))
            } else {
                queue.append(current)
            }
        }
        presenter.show("背完了！接下来开始测验！")
        await presenter.pause(seconds: 3)
        guard await runQuiz(words, allWords: allWords) else { return .cancelled }
        presenter.show("结束了！恭喜！")
        return .finished
    }

    // MARK: Random test (随机单词测试, UnorderWord)

    /// A translation quiz over `options.number` random words of the book, learned or not.
    public func runRandomTest() async throws -> StudyOutcome {
        let all = try database.allWords(book: options.book)
        let list = Array(all.shuffled(using: &random).prefix(max(0, options.number)))
        studiedWords = list
        guard !list.isEmpty else { return .noWords }
        var rng = random  // a stored property can't be passed inout across an await; copies share the stream
        defer { random = rng }
        let finished = await runRandomQuiz(list, presenter: presenter, random: &rng) { word, random in
            let others = all.filter { $0.wordRank != word.wordRank && $0.headWord != word.headWord }
            guard others.count >= 2 else { return nil }
            return StudyContent.translationQuestion(
                word, distractors: others.shuffled(using: &random).prefix(2).map(\.headWord),
                answerIndex: Int.random(in: 0..<3, using: &random))
        }
        guard finished else { return .cancelled }
        presenter.show("结束了！恭喜！")
        return .finished
    }

    // MARK: Quiz (PushWaitAllQuestions)

    /// 中译英 for words without a built-in question, then 选择题 for the rest. False when cancelled.
    public func runQuiz(_ words: [Word], allWords: [Word]) async -> Bool {
        var queue = words.filter { $0.question.isEmpty }
        while let word = queue.first {
            let others = allWords.filter { $0.wordRank != word.wordRank && $0.headWord != word.headWord }
            guard others.count >= 2 else { queue.removeFirst(); continue }
            let distractors = Array(others.shuffled(using: &random).prefix(2)).map(\.headWord)
            let question = StudyContent.translationQuestion(
                word, distractors: distractors, answerIndex: Int.random(in: 0..<3, using: &random))
            guard let correct = await ask(question) else { return false }
            queue.removeFirst()
            if correct {
                await presenter.pause(seconds: 0.5)
            } else {
                presenter.show(question.wrongMessage)
                queue.append(word)
                await presenter.pause(seconds: 5)
            }
        }

        var fills = words.compactMap { word in StudyContent.fillQuestion(word).map { (word, $0) } }
        while let (word, question) = fills.first {
            guard let correct = await ask(question) else { return false }
            fills.removeFirst()
            if !correct {
                presenter.show(question.wrongMessage)
                fills.append((word, question))
                await presenter.pause(seconds: 6)
            }
        }
        return !Task.isCancelled
    }

    private func ask(_ question: StudyContent.Question) async -> Bool? {
        while true {
            guard let answer = await presenter.ask(question.prompt) else { return nil }
            // On macOS a body click is often just an attempt to reveal the buttons; ask again.
            if answer != defaultActionID { return answer == String(question.correctIndex) }
        }
    }
}
