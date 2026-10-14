import Foundation
import Testing
@testable import ToastFishCore

private let bundledDatabase = URL(fileURLWithPath: #filePath)
    .deletingLastPathComponent().deletingLastPathComponent()
    .deletingLastPathComponent().deletingLastPathComponent()
    .appendingPathComponent("Resources/inami.db")

private func temporaryDatabase() throws -> (Database, URL) {
    let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    let url = try Database.prepareUserDatabase(bundledURL: bundledDatabase, appSupportDir: directory)
    return (try Database(url: url), directory)
}

/// Deterministic SplitMix64 so shuffles and answer positions repeat.
private func seeded(_ seed: UInt64) -> StudyRandom {
    var state = seed
    return StudyRandom {
        state &+= 0x9E37_79B9_7F4A_7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        return z ^ (z >> 31)
    }
}

/// Answers prompts with a closure and records everything shown.
@MainActor
private final class ScriptedPresenter: StudyPresenter {
    var prompts: [StudyPrompt] = []
    var messages: [String] = []
    var pronounced: [String] = []
    var answer: (StudyPrompt) -> String?

    init(answer: @escaping (StudyPrompt) -> String?) { self.answer = answer }

    func ask(_ prompt: StudyPrompt) async -> String? {
        prompts.append(prompt)
        return answer(prompt)
    }
    func show(_ message: String) { messages.append(message) }
    func pause(seconds: Double) async {}
    func pronounce(_ word: Word, engType: Int) { pronounced.append(word.headWord) }
    func speakJapanese(_ text: String) { pronounced.append(text) }
    func playGoin(_ romaji: String) { pronounced.append(romaji) }
}

/// Answers any three-choice quiz by matching the expected answer text.
@MainActor
private func pick(_ prompt: StudyPrompt, _ answer: String) -> String {
    prompt.actions.first { String($0.title.dropFirst(2)) == answer }!.id
}

/// Picks the right answer for quiz prompts by reading the word data back.
@MainActor
private func correctQuizAnswer(_ prompt: StudyPrompt, words: [Word]) -> String {
    if prompt.title == "翻译" {
        // Several words can share a translation; pick the choice that is one of them.
        let candidates = Set(words.filter { $0.tranCN == prompt.body }.map(\.headWord))
        return prompt.actions.first { candidates.contains(String($0.title.dropFirst(2))) }!.id
    }
    let word = words.first { $0.question == prompt.body }!
    return String(word.rightIndex! - 1)
}

@Test func translationQuestionPlacesAnswerLikeCSharp() {
    var word = Word(wordRank: 1)
    word.headWord = "absorb"
    word.tranCN = "吸收"
    let q0 = StudyContent.translationQuestion(word, distractors: ["b", "c"], answerIndex: 0)
    #expect(q0.prompt.actions.map(\.title) == ["A.absorb", "B.b", "C.c"])
    let q1 = StudyContent.translationQuestion(word, distractors: ["b", "c"], answerIndex: 1)
    #expect(q1.prompt.actions.map(\.title) == ["A.b", "B.absorb", "C.c"] && q1.correctIndex == 1)
    let q2 = StudyContent.translationQuestion(word, distractors: ["b", "c"], answerIndex: 2)
    #expect(q2.prompt.actions.map(\.title) == ["A.c", "B.b", "C.absorb"])
    #expect(q2.wrongMessage == "错误 正确答案：C.absorb")
}

@Test func fillQuestionUsesRightIndexAndRejectsBadData() throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let absorb = try #require(db.allWords(book: "CET4_1").first { $0.headWord == "absorb" })
    let question = try #require(StudyContent.fillQuestion(absorb))
    #expect(question.correctIndex == 1)
    #expect(question.prompt.actions.map(\.title) == ["A.attracted", "B.absorbed", "C.drawn", "D.concentrated"])
    #expect(question.wrongMessage.hasPrefix("错误, 正确答案：B\n"))
    var broken = absorb
    broken.rightIndex = 7
    #expect(StudyContent.fillQuestion(broken) == nil)
}

@Test func sm2StatusLinesMatchOriginal() {
    #expect(StudyContent.sm2StatusLine(.new, new: 3, learning: 1, reviewed: 2) == "状态：新开  新:3 背:1 复:2")
    #expect(StudyContent.sm2StatusLine(.step2, new: 0, learning: 0, reviewed: 0) == "状态：新学-阶段2 新:0 背:0 复:0")
    #expect(StudyContent.sm2StatusLine(.relearnStep1, new: 0, learning: 0, reviewed: 0) == "状态：重学-阶段1 新:0 背:0 复:0")
}

@MainActor
@Test func sm2EasyAnswersFinishAndPersistWithoutQuiz() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let presenter = ScriptedPresenter { _ in "easy" }
    let session = EnglishStudySession(
        database: db, presenter: presenter,
        options: .init(book: "CET4_1", number: 5, engType: 2, autoPlay: true), random: seeded(1))
    #expect(try await session.runSM2() == .finished)
    #expect(presenter.prompts.count == 5)
    #expect(presenter.prompts.allSatisfy { $0.actions.map(\.id) == ["again", "hard", "good", "easy", "voice"] })
    #expect(presenter.pronounced.count == 5)  // autoPlay
    #expect(presenter.messages == ["结束了！恭喜！"])  // all easy, so nothing to quiz
    let learned = try db.allWords(book: "CET4_1").filter { $0.status == CardStatus.reviewed.rawValue }
    #expect(Set(learned.map(\.wordRank)) == Set(session.studiedWords.map(\.wordRank)) && learned.count == 5)
    #expect(try db.selectCount(book: "CET4_1").current == 5)
}

@MainActor
@Test func sm2GoodAnswersRepeatCardsThenQuizUntilCorrect() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let all = try db.allWords(book: "CET4_1")
    var wrongOnce = true
    let presenter = ScriptedPresenter { prompt in
        if prompt.actions.first?.id == "again" { return "good" }
        // Miss the very first quiz question once to exercise the retry path.
        let right = correctQuizAnswer(prompt, words: all)
        if wrongOnce {
            wrongOnce = false
            return prompt.actions.first { $0.id != right }!.id
        }
        return right
    }
    let session = EnglishStudySession(
        database: db, presenter: presenter,
        options: .init(book: "CET4_1", number: 3, engType: 1, autoPlay: false), random: seeded(2))
    #expect(try await session.runSM2() == .finished)
    // good on a new card -> step2, good again -> reviewed: two SM2 prompts per word.
    let sm2Prompts = presenter.prompts.filter { $0.actions.first?.id == "again" }
    #expect(sm2Prompts.count == 6)
    #expect(presenter.messages.first == "背完了！接下来开始测验记忆模糊的单词！")
    #expect(presenter.messages.last == "结束了！恭喜！")
    #expect(presenter.messages.contains { $0.hasPrefix("错误") })
    let quizPrompts = presenter.prompts.count - sm2Prompts.count
    #expect(quizPrompts == 4)  // three words plus one retry
    #expect(try db.allWords(book: "CET4_1").filter { $0.status == CardStatus.reviewed.rawValue }.count == 3)
}

@MainActor
@Test func voiceAndBodyClickReplayWithoutGrading() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    var answers = ["voice", "default", "easy"]
    let presenter = ScriptedPresenter { _ in answers.isEmpty ? "easy" : answers.removeFirst() }
    let session = EnglishStudySession(
        database: db, presenter: presenter,
        options: .init(book: "CET4_1", number: 1, engType: 2, autoPlay: false), random: seeded(3))
    #expect(try await session.runSM2() == .finished)
    #expect(presenter.prompts.count == 3)
    #expect(presenter.pronounced.count == 2)
    #expect(Set(presenter.prompts.map(\.title)).count == 1)
}

@MainActor
@Test func cancellingMidSessionSavesFinishedCardsOnly() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    var asked = 0
    // Two words become reviewed, then the session is cancelled on the third prompt.
    let presenter = ScriptedPresenter { _ in
        asked += 1
        return asked <= 2 ? "easy" : nil
    }
    let session = EnglishStudySession(
        database: db, presenter: presenter,
        options: .init(book: "CET4_1", number: 5, engType: 2, autoPlay: false), random: seeded(4))
    #expect(try await session.runSM2() == .cancelled)
    #expect(try db.allWords(book: "CET4_1").filter { $0.status == CardStatus.reviewed.rawValue }.count == 2)
    #expect(try db.selectCount(book: "CET4_1").current == 2)
    #expect(presenter.messages.isEmpty)
}

@MainActor
@Test func overdueReviewsFollowNewCards() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    // First session: learn 2 words.
    let first = EnglishStudySession(database: db, presenter: ScriptedPresenter { _ in "easy" },
                                    options: .init(book: "CET4_1", number: 2, engType: 2, autoPlay: false),
                                    random: seeded(5))
    _ = try await first.runSM2()
    let learned = Set(first.studiedWords.map(\.headWord))
    // Second session: 2 new cards, then up to 4 reviewed cards (the 2 from before).
    let presenter = ScriptedPresenter { _ in "easy" }
    let second = EnglishStudySession(database: db, presenter: presenter,
                                     options: .init(book: "CET4_1", number: 2, engType: 2, autoPlay: false),
                                     random: seeded(6))
    _ = try await second.runSM2()
    let titles = presenter.prompts.map { String($0.title.split(separator: " ").first!) }
    #expect(titles.count == 4)
    #expect(Set(titles.suffix(2)) == learned)
    #expect(presenter.prompts.suffix(2).allSatisfy { $0.body.contains("状态：复习") })
}

@MainActor
@Test func recitationMovesSkippedWordsAndPersistsRemembered() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let all = try db.allWords(book: "CET4_1")
    let words = Array(all.prefix(3))
    var skipped = Set<String>()
    let presenter = ScriptedPresenter { prompt in
        if prompt.actions.first?.id == "succeed" {
            // Skip each word once, then remember it.
            if skipped.insert(prompt.title).inserted { return "fail" }
            return "succeed"
        }
        return correctQuizAnswer(prompt, words: all)
    }
    let session = EnglishStudySession(database: db, presenter: presenter,
                                      options: .init(book: "CET4_1", number: 3, engType: 2, autoPlay: false),
                                      random: seeded(7))
    #expect(try await session.runRecitation(words, allWords: all, persist: true) == .finished)
    let recitations = presenter.prompts.filter { $0.actions.first?.id == "succeed" }
    #expect(recitations.count == 6)
    // A skipped word is never shown again immediately while other words remain.
    for (a, b) in zip(recitations, recitations.dropFirst()).dropLast() { #expect(a.title != b.title) }
    let ranks = Set(words.map(\.wordRank))
    #expect(try db.allWords(book: "CET4_1").filter { ranks.contains($0.wordRank) }.allSatisfy { $0.status == 1 })
    #expect(presenter.messages == ["背完了！接下来开始测验！", "结束了！恭喜！"])
}

@MainActor
@Test func quizBodyClickAsksAgainAndTinyPoolsSkipTranslation() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let all = try db.allWords(book: "CET4_1")
    var plain = try #require(all.first { $0.question.isEmpty })
    plain.tranCN = "唯一的翻译"
    var clicks = 0
    let presenter = ScriptedPresenter { prompt in
        clicks += 1
        return clicks == 1 ? defaultActionID : prompt.actions.first { $0.title.hasSuffix(".\(plain.headWord)") }!.id
    }
    let session = EnglishStudySession(database: db, presenter: presenter,
                                      options: .init(book: "CET4_1", number: 1, engType: 2, autoPlay: false),
                                      random: seeded(8))
    #expect(await session.runQuiz([plain], allWords: all))
    #expect(presenter.prompts.count == 2 && presenter.messages.isEmpty)
    // Fewer than two other words: no translation question can be built, so nothing is asked.
    presenter.prompts = []
    #expect(await session.runQuiz([plain], allWords: [plain]))
    #expect(presenter.prompts.isEmpty)
}

@MainActor
@Test func singleWordRecitationRepeatsAndReplaysVoice() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    // No built-in question and no other words, so the quiz has nothing to ask.
    let word = try #require(db.allWords(book: "CET4_1").first { $0.question.isEmpty })
    var answers = ["voice", "fail", "succeed"]
    let presenter = ScriptedPresenter { prompt in
        if prompt.actions.first?.id == "succeed" { return answers.removeFirst() }
        return nil
    }
    let session = EnglishStudySession(database: db, presenter: presenter,
                                      options: .init(book: "CET4_1", number: 1, engType: 2, autoPlay: false),
                                      random: seeded(9))
    #expect(try await session.runRecitation([word], allWords: [word], persist: false) == .finished)
    #expect(presenter.prompts.count == 3 && presenter.pronounced == [word.headWord])
    #expect(try db.allWords(book: "CET4_1").first { $0.wordRank == word.wordRank }!.status == word.status)
}

@MainActor
@Test func requestingZeroWordsReportsNoWords() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let presenter = ScriptedPresenter { _ in "easy" }
    let session = EnglishStudySession(database: db, presenter: presenter,
                                      options: .init(book: "CET4_1", number: 0, engType: 2, autoPlay: false))
    #expect(try await session.runSM2() == .noWords)
    #expect(presenter.messages == ["好..好像词库里没有单词了，您就是摸鱼之王！"])
}

@Test func goinQuestionsAskTheRightScript() {
    let a = GoinWord(wordRank: 1, bookID: "goin", status: 0, hiragana: "あ", romaji: "a", katakana: "ア")
    let i = GoinWord(wordRank: 2, bookID: "goin", status: 0, hiragana: "い", romaji: "i", katakana: "イ")
    let u = GoinWord(wordRank: 3, bookID: "goin", status: 0, hiragana: "う", romaji: "u", katakana: "ウ")
    let q0 = StudyContent.goinQuestion(a, distractors: [i, u], type: 0, answerIndex: 2)
    #expect(q0.prompt.title == "选择平假名" && q0.prompt.body == "a")
    #expect(q0.prompt.actions.map(\.title) == ["A.う", "B.い", "C.あ"] && q0.correctIndex == 2)
    let q1 = StudyContent.goinQuestion(a, distractors: [i, u], type: 1, answerIndex: 0)
    #expect(q1.prompt.body == "あ" && q1.prompt.actions.map(\.title) == ["A.ア", "B.イ", "C.ウ"])
    let q2 = StudyContent.goinQuestion(a, distractors: [i, u], type: 2, answerIndex: 1)
    #expect(q2.prompt.title == "选择片假名" && q2.prompt.body == "a" && q2.wrongMessage == "错误 正确答案：B.ア")
}

@MainActor
@Test func orderedGoinAdvancesAndWrapsSavedPosition() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let all = try db.allGoinWords()
    try db.setGoinProgress(all.count - 1)  // two kana before the end
    var voiced = false
    let presenter = ScriptedPresenter { prompt in
        if prompt.actions.map(\.id) == ["succeed", "voice"] {
            if !voiced { voiced = true; return "voice" }
            return "succeed"
        }
        let romaji = prompt.body
        let word = all.first { $0.romaji == romaji || $0.hiragana == romaji }!
        return pick(prompt, prompt.title == "选择平假名" ? word.hiragana : word.katakana)
    }
    let session = JapaneseStudySession(database: db, presenter: presenter, random: seeded(10))
    #expect(try await session.runOrderedGoin(number: 3) == .finished)
    #expect(session.studiedGoin.map(\.wordRank) == [all.count - 1, all.count, 1])
    #expect(try db.goinProgress() == 2)
    #expect(presenter.pronounced == [all[all.count - 2].romaji])
    #expect(presenter.messages == ["背完了！接下来开始测验！", "结束了！恭喜！"])
}

@MainActor
@Test func orderedGoinTreatsResetPositionAsFirstKana() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    try db.setGoinProgress(0)
    let presenter = ScriptedPresenter { _ in nil }
    let session = JapaneseStudySession(database: db, presenter: presenter)
    #expect(try await session.runOrderedGoin(number: 1) == .cancelled)
    #expect(presenter.prompts.first?.title == "平假名：あ 片假名：ア")
}

@MainActor
@Test func japaneseRecitationPersistsAndQuizzes() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let all = try db.allJpWords()
    var wrongOnce = true
    var voicedOnce = false
    let presenter = ScriptedPresenter { prompt in
        if prompt.actions.first?.id == "succeed" {
            if !voicedOnce { voicedOnce = true; return "voice" }
            return "succeed"
        }
        let candidates = Set(all.filter { $0.tranCN == prompt.body }.map(\.headWord))
        let right = prompt.actions.first { candidates.contains(String($0.title.dropFirst(2))) }!.id
        if wrongOnce { wrongOnce = false; return prompt.actions.first { $0.id != right }!.id }
        return right
    }
    let session = JapaneseStudySession(database: db, presenter: presenter, random: seeded(11))
    #expect(try await session.runRecitation(number: 3) == .finished)
    #expect(session.studiedWords.count == 3)
    let ranks = Set(session.studiedWords.map(\.wordRank))
    #expect(try db.allJpWords().filter { ranks.contains($0.wordRank) }.allSatisfy { $0.status == 1 })
    #expect(try db.selectCount(book: "StdJp_Mid").current == 3)
    #expect(presenter.messages.contains { $0.hasPrefix("错误 正确答案：") })
    #expect(presenter.messages.last == "结束了！恭喜！")
    #expect(presenter.pronounced.count == 1)
}

@MainActor
@Test func randomTestsDoNotChangeProgress() async throws {
    let (db, dir) = try temporaryDatabase()
    defer { try? FileManager.default.removeItem(at: dir) }
    let english = try db.allWords(book: "CET4_1")
    let goin = try db.allGoinWords()
    let japanese = try db.allJpWords()
    let presenter = ScriptedPresenter { prompt in
        if prompt.title == "翻译" {
            let candidates = Set(english.filter { $0.tranCN == prompt.body }.map(\.headWord)
                                 + japanese.filter { $0.tranCN == prompt.body }.map(\.headWord))
            return prompt.actions.first { candidates.contains(String($0.title.dropFirst(2))) }!.id
        }
        let word = goin.first { $0.romaji == prompt.body || $0.hiragana == prompt.body }!
        return pick(prompt, prompt.title == "选择平假名" ? word.hiragana : word.katakana)
    }
    let englishSession = EnglishStudySession(database: db, presenter: presenter,
        options: .init(book: "CET4_1", number: 4, engType: 2, autoPlay: false), random: seeded(12))
    #expect(try await englishSession.runRandomTest() == .finished)
    let japaneseSession = JapaneseStudySession(database: db, presenter: presenter, random: seeded(13))
    #expect(try await japaneseSession.runRandomGoin(number: 4) == .finished)
    #expect(try await japaneseSession.runRandomTest(number: 4) == .finished)
    #expect(presenter.prompts.count == 12)  // every answer was right
    #expect(try db.selectCount(book: "CET4_1").current == 0)
    #expect(try db.goinProgress() == 1)
}

@MainActor
@Test func customRecitationRequeuesSkippedCards() async {
    let cards = [CustomizeWord(firstLine: "一", secondLine: "1"), CustomizeWord(firstLine: "二", secondLine: "2")]
    var seen = 0
    let presenter = ScriptedPresenter { _ in
        seen += 1
        return seen == 1 ? "fail" : "succeed"
    }
    #expect(await runCustomRecitation(cards, presenter: presenter) == .finished)
    #expect(presenter.prompts.map(\.title) == ["一\n1", "二\n2", "一\n1"])
    #expect(presenter.messages == ["背完了！"])
}
