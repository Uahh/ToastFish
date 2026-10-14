import Foundation

/// The Excel layouts of CreateLog.cs: cell A1 names the type, data starts in column B on row 2.
public enum WordListIO {
    public enum Imported: Equatable, Sendable {
        case english([Word])
        case japanese([JpWord])
        case custom([CustomizeWord])
    }

    public enum ImportError: Error, LocalizedError, Equatable {
        case unknownFormat
        case empty

        public var errorDescription: String? {
            switch self {
            case .unknownFormat: return "导入失败，Excel格式不正确或Excel文件损坏"
            case .empty: return "Excel 里没有可以背的内容"
            }
        }
    }

    static let englishHeader = ["英语", "单词", "翻译", "英音", "美音", "词性", "短语", "短语翻译", "例句", "例句翻译",
                                "问题", "问题翻译", "选项A", "选项B", "选项C", "选项D", "正确答案"]
    static let japaneseHeader = ["日语", "单词", "翻译", "读音", "假名", "词性"]
    static let goinHeader = ["五十音", "罗马音", "平假名", "片假名"]

    // MARK: Export (session logs)

    public static func export(_ words: [Word]) -> Data {
        sheet(englishHeader, words.map {
            [$0.headWord, $0.tranCN, $0.ukPhone, $0.usPhone, $0.pos, $0.phrase, $0.phraseCN, $0.sentence, $0.sentenceCN,
             $0.question, $0.explain, $0.choiceIndexOne, $0.choiceIndexTwo, $0.choiceIndexThree, $0.choiceIndexFour,
             $0.rightIndex.map(String.init) ?? ""]
        })
    }

    public static func export(_ words: [JpWord]) -> Data {
        sheet(japaneseHeader, words.map { [$0.headWord, $0.tranCN, String($0.phone), $0.hiragana, $0.pos] })
    }

    public static func export(_ words: [GoinWord]) -> Data {
        sheet(goinHeader, words.map { [$0.romaji, $0.hiragana, $0.katakana] })
    }

    private static func sheet(_ header: [String], _ rows: [[String]]) -> Data {
        let widths = Dictionary(uniqueKeysWithValues: (1..<header.count).map { ($0, 20.0) })
        return XLSX.write(rows: [header] + rows.map { [""] + $0 }, columnWidths: widths)
    }

    // MARK: Import (导入单词)

    public static func importRows(_ rows: [[String]]) throws -> Imported {
        guard let kind = rows.first?.first?.trimmingCharacters(in: .whitespaces) else { throw ImportError.unknownFormat }
        // Rows without a first field are skipped, like empty Excel rows were.
        let body = rows.dropFirst().filter { !cell($0, 1).trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        let result: Imported
        switch kind {
        case "英语":
            result = .english(body.enumerated().map { index, row in
                // Imported words get distinct ranks so quizzes can tell them apart; they are never saved.
                var word = Word(wordRank: -(index + 1), bookID: "", status: 0)
                word.headWord = cell(row, 1); word.tranCN = cell(row, 2)
                word.ukPhone = cell(row, 3); word.usPhone = cell(row, 4); word.pos = cell(row, 5)
                word.phrase = cell(row, 6); word.phraseCN = cell(row, 7)
                word.sentence = cell(row, 8); word.sentenceCN = cell(row, 9)
                word.question = cell(row, 10); word.explain = cell(row, 11)
                word.choiceIndexOne = cell(row, 12); word.choiceIndexTwo = cell(row, 13)
                word.choiceIndexThree = cell(row, 14); word.choiceIndexFour = cell(row, 15)
                word.rightIndex = number(cell(row, 16))
                return word
            })
        case "日语":
            result = .japanese(body.enumerated().map { index, row in
                JpWord(wordRank: -(index + 1), bookID: "", status: 0, headWord: cell(row, 1), phone: number(cell(row, 3)) ?? -1,
                       tranCN: cell(row, 2), hiragana: cell(row, 4), pos: cell(row, 5))
            })
        case "自定义":
            result = .custom(body.map {
                CustomizeWord(firstLine: cell($0, 1), secondLine: cell($0, 2), thirdLine: cell($0, 3), fourthLine: cell($0, 4))
            })
        default:
            throw ImportError.unknownFormat
        }
        guard body.isEmpty == false else { throw ImportError.empty }
        return result
    }

    public static func importFile(_ data: Data) throws -> Imported {
        try importRows(XLSX.readFirstSheet(data))
    }

    private static func cell(_ row: [String], _ index: Int) -> String {
        row.indices.contains(index) ? row[index] : ""  // kept verbatim, as NPOI's ToString() did
    }

    /// Excel stores numbers like "3" or "3.0".
    private static func number(_ text: String) -> Int? {
        Int(text) ?? Double(text).flatMap { $0.rounded() == $0 ? Int($0) : nil }
    }
}

/// Writes session logs like the original Log folder, named "<date>_<kind>.xlsx".
public enum SessionLog {
    public static func fileName(kind: String, date: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd_HH-mm-ss"
        return "\(formatter.string(from: date))_\(kind).xlsx"
    }

    @discardableResult
    public static func write(_ data: Data, kind: String, to directory: URL, date: Date = Date()) throws -> URL {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let url = directory.appendingPathComponent(fileName(kind: kind, date: date))
        try data.write(to: url, options: .atomic)
        return url
    }
}
