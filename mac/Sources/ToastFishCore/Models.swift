/// Database TEXT NULLs are consistently represented by an empty string.
public struct Word: Sendable, Equatable {
    public var wordRank: Int
    public var bookID: String
    public var status: Int
    public var headWord = ""
    public var usPhone = ""
    public var ukPhone = ""
    public var usSpeech = ""
    public var ukSpeech = ""
    public var tranCN = ""
    public var pos = ""
    public var tranOther = ""
    public var question = ""
    public var explain = ""
    public var rightIndex: Int?
    public var examType: Int?
    public var choiceIndexOne = ""
    public var choiceIndexTwo = ""
    public var choiceIndexThree = ""
    public var choiceIndexFour = ""
    public var sentence = ""
    public var sentenceCN = ""
    public var phrase = ""
    public var phraseCN = ""
    public var difficulty = Parameters.difficultyDefaultValue
    public var daysBetweenReviews = Parameters.daysBetweenReviewsDefaultValue
    public var lastScore = Parameters.none
    public var dateLastReviewed = ""

    public init(wordRank: Int = 0, bookID: String = "CET4_1", status: Int = 0) {
        self.wordRank = wordRank
        self.bookID = bookID
        self.status = status
    }
}

public struct JpWord: Sendable, Equatable {
    public var wordRank: Int
    public var bookID: String
    public var status: Int
    public var headWord: String
    public var phone: Int
    public var tranCN: String
    public var hiragana: String
    public var pos: String
}

public struct GoinWord: Sendable, Equatable {
    public var wordRank: Int
    public var bookID: String
    public var status: Int
    public var hiragana: String
    public var romaji: String
    public var katakana: String
}

public struct BookCount: Sendable, Equatable {
    public var bookName: String
    public var number: Int
    public var current: Int
}

public struct GlobalConfig: Sendable, Equatable {
    public var currentWordNumber: Int
    public var currentBookName: String
    public var autoPlay: Bool
    public var engType: Int
    public var autoLog: Bool

    public init(currentWordNumber: Int = 10, currentBookName: String = "CET4_1",
                autoPlay: Bool = true, engType: Int = 2, autoLog: Bool = true) {
        self.currentWordNumber = currentWordNumber
        self.currentBookName = currentBookName
        self.autoPlay = autoPlay
        self.engType = engType
        self.autoLog = autoLog
    }
}

public struct CustomizeWord: Sendable, Equatable {
    public var firstLine: String
    public var secondLine: String
    public var thirdLine: String
    public var fourthLine: String

    public init(firstLine: String = "", secondLine: String = "",
                thirdLine: String = "", fourthLine: String = "") {
        self.firstLine = firstLine
        self.secondLine = secondLine
        self.thirdLine = thirdLine
        self.fourthLine = fourthLine
    }
}

/// Learning batch payload, matching PushControl/WordType.cs.
public struct WordType: Sendable {
    public var number = 0
    public var wordList: [Word]?
    public var jpWordList: [JpWord]?
    public var custWordList: [CustomizeWord]?
    public var goinWordList = false

    public init() {}
}
