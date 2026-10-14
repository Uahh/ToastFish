import Foundation

/// The "pick a random item until every one is answered correctly" quiz loop shared by
/// UnorderWord, PushJpWords and PushGoinWords. A wrong answer keeps the item in the pool.
@MainActor
func runRandomQuiz<Item>(_ items: [Item], presenter: StudyPresenter, random: inout StudyRandom,
                         question: (Item, inout StudyRandom) -> StudyContent.Question?) async -> Bool {
    var pool = items
    while !pool.isEmpty {
        let index = Int.random(in: 0..<pool.count, using: &random)
        guard let question = question(pool[index], &random) else {
            pool.remove(at: index)
            continue
        }
        var answer: String?
        repeat {
            guard let next = await presenter.ask(question.prompt) else { return false }
            answer = next
        } while answer == defaultActionID  // a body click just reveals the buttons on macOS
        if answer == String(question.correctIndex) {
            pool.remove(at: index)
            await presenter.pause(seconds: 0.5)
        } else {
            presenter.show(question.wrongMessage)
            await presenter.pause(seconds: 3)
        }
    }
    return !Task.isCancelled
}

/// 标准日本语 words and 五十音, ported from PushJpWords.cs and PushGoinWords.cs.
@MainActor
public final class JapaneseStudySession {
    private let database: Database
    private let presenter: StudyPresenter
    private var random: StudyRandom

    /// What was studied in the last run, for the session log.
    public private(set) var studiedWords: [JpWord] = []
    public private(set) var studiedGoin: [GoinWord] = []

    public init(database: Database, presenter: StudyPresenter, random: StudyRandom = .system) {
        self.database = database
        self.presenter = presenter
        self.random = random
    }

    // MARK: Japanese words

    /// PushJpWords.Recitation. Pass `words` for an imported list (not saved), nil to draw `number` unseen words.
    public func runRecitation(words: [JpWord]? = nil, number: Int) async throws -> StudyOutcome {
        let persist = words == nil
        let list = try words ?? database.randomJpWords(n: number)
        studiedWords = list
        guard !list.isEmpty else {
            if persist { presenter.show("好..好像词库里没有单词了，您就是摸鱼之王！") }
            return .noWords
        }
        var pool = list
        while !pool.isEmpty {
            // The original picks a random remaining word each time instead of reordering.
            let index = Int.random(in: 0..<pool.count, using: &random)
            let word = pool[index]
            var remembered: Bool?
            while remembered == nil {
                guard let answer = await presenter.ask(StudyContent.jpRecitationPrompt(word)) else { return .cancelled }
                switch answer {
                case "succeed": remembered = true
                case "voice": presenter.speakJapanese(word.hiragana.isEmpty ? word.headWord : word.hiragana)
                default: remembered = false
                }
            }
            if remembered == true {
                if persist {
                    try database.markWordLearned(book: "StdJp_Mid", wordRank: word.wordRank)
                    try database.updateTableCount(book: "StdJp_Mid")
                }
                pool.remove(at: index)
            }
        }
        presenter.show("背完了！接下来开始测验！")
        await presenter.pause(seconds: 3)
        guard try await quiz(list) else { return .cancelled }
        presenter.show("结束了！恭喜！")
        return .finished
    }

    /// PushJpWords.UnorderWord (随机日语单词测试): a translation quiz over random words, learned or not.
    public func runRandomTest(number: Int) async throws -> StudyOutcome {
        let list = Array(try database.allJpWords().shuffled(using: &random).prefix(max(0, number)))
        studiedWords = list
        guard !list.isEmpty else { return .noWords }
        guard try await quiz(list) else { return .cancelled }
        presenter.show("结束了！恭喜！")
        return .finished
    }

    private func quiz(_ words: [JpWord]) async throws -> Bool {
        let all = try database.allJpWords()
        var rng = random  // a stored property can't be passed inout across an await; copies share the stream
        defer { random = rng }
        return await runRandomQuiz(words, presenter: presenter, random: &rng) { word, random in
            let others = all.filter { $0.wordRank != word.wordRank && $0.headWord != word.headWord }
            guard others.count >= 2 else { return nil }
            return StudyContent.translationQuestion(
                word, distractors: others.shuffled(using: &random).prefix(2).map(\.headWord),
                answerIndex: Int.random(in: 0..<3, using: &random))
        }
    }

    // MARK: 五十音

    /// PushGoinWords.OrderGoin: `number` kana in table order from the saved position, then a quiz on them.
    public func runOrderedGoin(number: Int) async throws -> StudyOutcome {
        let all = try database.allGoinWords()
        guard !all.isEmpty, number > 0 else { return .noWords }
        // Positions are 1-based and wrap after the last kana; a reset (0) starts from the first.
        var position = min(max(try database.goinProgress(), 1), all.count)
        var learned: [GoinWord] = []
        while learned.count < number {
            let word = all[position - 1]
            guard let answer = await presenter.ask(StudyContent.goinPrompt(word)) else {
                studiedGoin = learned
                return .cancelled
            }
            switch answer {
            case "succeed":
                learned.append(word)
                position = position % all.count + 1
                try database.setGoinProgress(position)
            case "voice":
                presenter.playGoin(word.romaji)
            default:
                break  // a body click shows the same kana again, as C# treated it as "fail"
            }
        }
        studiedGoin = learned
        presenter.show("背完了！接下来开始测验！")
        await presenter.pause(seconds: 3)
        guard await goinQuiz(learned, all: all) else { return .cancelled }
        presenter.show("结束了！恭喜！")
        return .finished
    }

    /// PushGoinWords.UnorderGoin (随机五十音测试).
    public func runRandomGoin(number: Int) async throws -> StudyOutcome {
        let all = try database.allGoinWords()
        let list = Array(all.shuffled(using: &random).prefix(max(0, number)))
        studiedGoin = list
        guard !list.isEmpty else { return .noWords }
        guard await goinQuiz(list, all: all) else { return .cancelled }
        presenter.show("结束了！恭喜！")
        return .finished
    }

    private func goinQuiz(_ words: [GoinWord], all: [GoinWord]) async -> Bool {
        var rng = random  // a stored property can't be passed inout across an await; copies share the stream
        defer { random = rng }
        return await runRandomQuiz(words, presenter: presenter, random: &rng) { word, random in
            let others = all.filter { $0.wordRank != word.wordRank }
            guard others.count >= 2 else { return nil }
            return StudyContent.goinQuestion(
                word, distractors: Array(others.shuffled(using: &random).prefix(2)),
                type: Int.random(in: 0..<3, using: &random), answerIndex: Int.random(in: 0..<3, using: &random))
        }
    }
}

/// PushCustomizeWords.Recitation: free-form cards from the custom Excel template, no quiz, nothing saved.
@MainActor
public func runCustomRecitation(_ words: [CustomizeWord], presenter: StudyPresenter) async -> StudyOutcome {
    guard !words.isEmpty else { return .noWords }
    var queue = words
    while let word = queue.first {
        guard let answer = await presenter.ask(StudyContent.customPrompt(word)) else { return .cancelled }
        queue.removeFirst()
        if answer != "succeed" { queue.append(word) }
    }
    presenter.show("背完了！")
    return .finished
}
