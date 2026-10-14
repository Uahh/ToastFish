import Foundation

/// Notification text and quiz construction, ported from PushWords.cs.
public enum StudyContent {
    static let answerLetters = ["A", "B", "C", "D"]

    /// "headWord  (phone)\npos. tranCN" plus a short example or phrase (PushOneWord).
    public static func wordLines(_ word: Word, engType: Int) -> (title: String, body: String) {
        let phone = engType == 1 ? word.usPhone : word.ukPhone
        let title = phone.isEmpty ? word.headWord : "\(word.headWord)  (\(phone))"
        var body = "\(word.pos). \(word.tranCN)"
        if !word.sentence.isEmpty && word.sentence.count < 50 {
            body += "\n\(word.sentence)\n\(word.sentenceCN)"
        } else if !word.phrase.isEmpty {
            body += "\n\(word.phrase)\n\(word.phraseCN)"
        }
        return (title, body)
    }

    /// The status line of PushOneWordSM2.
    public static func sm2StatusLine(_ status: CardStatus, new: Int, learning: Int, reviewed: Int) -> String {
        let counts = " 新:\(new) 背:\(learning) 复:\(reviewed)"
        switch status {
        case .reviewed: return "状态：复习 " + counts
        case .new: return "状态：新开 " + counts
        case .step1, .step2: return "状态：新学-阶段\(status.rawValue)" + counts
        case .relearnStep1, .relearnStep2:
            return "状态：重学-阶段\(status.rawValue - CardStatus.step2.rawValue)" + counts
        }
    }

    public static func sm2Prompt(_ card: Card, engType: Int, new: Int, learning: Int, reviewed: Int) -> StudyPrompt {
        let lines = wordLines(card.word, engType: engType)
        return StudyPrompt(
            title: lines.title,
            body: lines.body + "\n" + sm2StatusLine(card.status, new: new, learning: learning, reviewed: reviewed),
            actions: [
                PromptAction(id: "again", title: "没有印象"),
                PromptAction(id: "hard", title: "记忆模糊"),
                PromptAction(id: "good", title: "暂时记住"),
                PromptAction(id: "easy", title: "已经牢记"),
                PromptAction(id: "voice", title: "发音"),
            ]
        )
    }

    public static func recitationPrompt(_ word: Word, engType: Int) -> StudyPrompt {
        let lines = wordLines(word, engType: engType)
        return StudyPrompt(title: lines.title, body: lines.body, actions: [
            PromptAction(id: "succeed", title: "记住了！"),
            PromptAction(id: "fail", title: "暂时跳过.."),
            PromptAction(id: "voice", title: "发音"),
        ])
    }

    /// A multiple-choice question; `correctIndex` is the index into `prompt.actions`.
    public struct Question: Sendable, Equatable {
        public var prompt: StudyPrompt
        public var correctIndex: Int
        public var wrongMessage: String
    }

    /// Three-choice question; the answer goes to `answerIndex` the way all C# Push*Question methods did:
    /// it swaps with whichever slot it lands on, so [A,B,C] becomes [B,A,C] or [C,B,A].
    public static func choiceQuestion(title: String, body: String, answer: String,
                                      distractors: [String], answerIndex: Int) -> Question {
        precondition(distractors.count == 2 && (0..<3).contains(answerIndex))
        var choices = [answer] + distractors
        choices.swapAt(0, answerIndex)
        let actions = choices.enumerated().map { index, choice in
            PromptAction(id: String(index), title: "\(answerLetters[index]).\(choice)")
        }
        return Question(
            prompt: StudyPrompt(title: title, body: body, actions: actions),
            correctIndex: answerIndex,
            wrongMessage: "错误 正确答案：\(answerLetters[answerIndex]).\(answer)"
        )
    }

    /// 中译英 (PushOneTransQuestion), used for English and Japanese words.
    public static func translationQuestion(_ word: Word, distractors: [String], answerIndex: Int) -> Question {
        choiceQuestion(title: "翻译", body: word.tranCN, answer: word.headWord,
                       distractors: distractors, answerIndex: answerIndex)
    }

    public static func translationQuestion(_ word: JpWord, distractors: [String], answerIndex: Int) -> Question {
        choiceQuestion(title: "翻译", body: word.tranCN, answer: word.headWord,
                       distractors: distractors, answerIndex: answerIndex)
    }

    /// PushOneGoinWordQuestion_1/2/3: romaji→平假名, 平假名→片假名, romaji→片假名.
    public static func goinQuestion(_ word: GoinWord, distractors: [GoinWord], type: Int, answerIndex: Int) -> Question {
        precondition(distractors.count == 2 && (0..<3).contains(type))
        switch type {
        case 0:
            return choiceQuestion(title: "选择平假名", body: word.romaji, answer: word.hiragana,
                                  distractors: distractors.map(\.hiragana), answerIndex: answerIndex)
        case 1:
            return choiceQuestion(title: "选择片假名", body: word.hiragana, answer: word.katakana,
                                  distractors: distractors.map(\.katakana), answerIndex: answerIndex)
        default:
            return choiceQuestion(title: "选择片假名", body: word.romaji, answer: word.katakana,
                                  distractors: distractors.map(\.katakana), answerIndex: answerIndex)
        }
    }

    /// PushJpWords.PushOneWord: "headWord  (hiragana)  重音：n\ntranCN" and the part of speech.
    public static func jpRecitationPrompt(_ word: JpWord) -> StudyPrompt {
        var title = "\(word.headWord)  (\(word.hiragana))"
        if word.phone != -1 { title += "  重音：\(word.phone)" }
        return StudyPrompt(title: title, body: word.tranCN + (word.pos.isEmpty ? "" : "\n\(word.pos)"), actions: [
            PromptAction(id: "succeed", title: "记住了！"),
            PromptAction(id: "fail", title: "暂时跳过.."),
            PromptAction(id: "voice", title: "发音"),
        ])
    }

    /// PushGoinWord: no skip button in the original, only 记住了 and 发音.
    public static func goinPrompt(_ word: GoinWord) -> StudyPrompt {
        StudyPrompt(title: "平假名：\(word.hiragana) 片假名：\(word.katakana)", body: "罗马音：\(word.romaji)", actions: [
            PromptAction(id: "succeed", title: "记住了！"),
            PromptAction(id: "voice", title: "发音"),
        ])
    }

    /// PushCustomizeWords.PushOneWord: four free-form lines from the custom Excel template.
    public static func customPrompt(_ word: CustomizeWord) -> StudyPrompt {
        StudyPrompt(title: [word.firstLine, word.secondLine].filter { !$0.isEmpty }.joined(separator: "\n"),
                    body: [word.thirdLine, word.fourthLine].filter { !$0.isEmpty }.joined(separator: "\n"),
                    actions: [
                        PromptAction(id: "succeed", title: "记住了！"),
                        PromptAction(id: "fail", title: "暂时跳过.."),
                    ])
    }

    /// 选择题 from the word's own question and four choices (PushOneQuestion). Nil when the data is unusable.
    public static func fillQuestion(_ word: Word) -> Question? {
        guard !word.question.isEmpty, let rightIndex = word.rightIndex, (1...4).contains(rightIndex) else { return nil }
        let choices = [word.choiceIndexOne, word.choiceIndexTwo, word.choiceIndexThree, word.choiceIndexFour]
        let actions = choices.enumerated().map { index, choice in
            PromptAction(id: String(index), title: "\(answerLetters[index]).\(choice)")
        }
        var wrong = "错误, 正确答案：\(answerLetters[rightIndex - 1])"
        if !word.explain.isEmpty { wrong += "\n\(word.explain)" }
        return Question(
            prompt: StudyPrompt(title: "选择题", body: word.question, actions: actions),
            correctIndex: rightIndex - 1,
            wrongMessage: wrong
        )
    }
}
