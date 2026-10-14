import Foundation
import Testing
@testable import ToastFishCore

private let repoResources = URL(fileURLWithPath: #filePath)
    .deletingLastPathComponent().deletingLastPathComponent()
    .deletingLastPathComponent().deletingLastPathComponent()
    .appendingPathComponent("Resources")

@Test func zipRoundTripsAndChecksumsMatchZlib() throws {
    #expect(ZipArchive.crc32(Data("123456789".utf8)) == 0xCBF4_3926)
    let archive = ZipArchive.write([("a.txt", Data("你好".utf8)), ("dir/b.bin", Data([0, 1, 2]))])
    let files = try ZipArchive.read(archive)
    #expect(files == ["a.txt": Data("你好".utf8), "dir/b.bin": Data([0, 1, 2])])
    #expect(throws: ZipError.self) { try ZipArchive.read(Data("not a zip at all, not even close".utf8)) }
}

@Test func writtenArchiveOpensWithSystemUnzip() throws {
    let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
    defer { try? FileManager.default.removeItem(at: dir) }
    let file = dir.appendingPathComponent("log.xlsx")
    var word = Word(wordRank: 1)
    word.headWord = "absorb"
    try WordListIO.export([word]).write(to: file)
    let unzip = Process()
    unzip.executableURL = URL(fileURLWithPath: "/usr/bin/unzip")
    unzip.arguments = ["-tq", file.path]
    unzip.standardOutput = FileHandle.nullDevice
    try unzip.run()
    unzip.waitUntilExit()
    #expect(unzip.terminationStatus == 0)
}

@Test func readsExcelMadeTemplateWithSharedStringsAndDeflate() throws {
    let data = try Data(contentsOf: repoResources.appendingPathComponent("自定义模板.xlsx"))
    let rows = try XLSX.readFirstSheet(data)
    #expect(rows[0] == ["自定义", "第一行内容", "第二行内容", "第三行内容", "第四行内容"])
    #expect(rows[1] == ["", "单词1", "自定义内容", "自定义内容", "自定义内容"])
    guard case .custom(let cards) = try WordListIO.importRows(rows) else {
        Issue.record("expected custom cards")
        return
    }
    // The original dropped the last row (i < RowCount); every filled row is imported here.
    #expect(cards.count == 15)
    #expect(cards.first == CustomizeWord(firstLine: "单词1", secondLine: "自定义内容", thirdLine: "自定义内容", fourthLine: "自定义内容"))
    #expect(cards.last?.firstLine == "单词15")
}

@Test func englishLogRoundTripsThroughImport() throws {
    let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    let url = try Database.prepareUserDatabase(bundledURL: repoResources.appendingPathComponent("inami.db"), appSupportDir: dir)
    defer { try? FileManager.default.removeItem(at: dir) }
    let words = Array(try Database(url: url).allWords(book: "CET4_1").prefix(5))
    guard case .english(let imported) = try WordListIO.importFile(WordListIO.export(words)) else {
        Issue.record("expected English words")
        return
    }
    #expect(imported.count == 5)
    for (original, copy) in zip(words, imported) {
        #expect(copy.headWord == original.headWord && copy.tranCN == original.tranCN)
        #expect(copy.ukPhone == original.ukPhone && copy.usPhone == original.usPhone)
        #expect(copy.question == original.question && copy.explain == original.explain)
        #expect(copy.choiceIndexFour == original.choiceIndexFour && copy.rightIndex == original.rightIndex)
        #expect(copy.sentenceCN == original.sentenceCN && copy.phraseCN == original.phraseCN)
    }
    #expect(Set(imported.map(\.wordRank)).count == 5)
}

@Test func japaneseImportParsesNumbersAndEscapes() throws {
    let rows = [["日语", "单词", "翻译", "读音", "假名", "词性"],
                ["", "出会い", "相遇 & <见面>", "0", "であい", "名词"],
                ["", "助かる", "得救", "3.0", "たすかる", "动1"],
                [],
                ["", "", "没有单词的行会被跳过"]]
    let data = XLSX.write(rows: rows)
    #expect(try XLSX.readFirstSheet(data) == rows.map { row in
        // Trailing empty cells are not stored.
        Array(row.prefix(through: row.lastIndex(where: { !$0.isEmpty }) ?? -1))
    })
    guard case .japanese(let words) = try WordListIO.importFile(data) else {
        Issue.record("expected Japanese words")
        return
    }
    #expect(words.map(\.headWord) == ["出会い", "助かる"])
    #expect(words.map(\.phone) == [0, 3])
    #expect(words[0].tranCN == "相遇 & <见面>")
}

@Test func rejectsUnknownAndEmptySheets() {
    #expect(throws: WordListIO.ImportError.unknownFormat) { try WordListIO.importRows([["随便什么"]]) }
    #expect(throws: WordListIO.ImportError.unknownFormat) { try WordListIO.importRows([]) }
    #expect(throws: WordListIO.ImportError.empty) { try WordListIO.importRows([["英语", "单词"]]) }
}

@Test func columnReferences() {
    #expect(XLSX.columnName(0) == "A" && XLSX.columnName(25) == "Z" && XLSX.columnName(26) == "AA")
    #expect(XLSX.columnIndex("B12") == 1 && XLSX.columnIndex("AA3") == 26 && XLSX.columnIndex("12") == nil)
}

@Test func logFileNamesAreSortable() {
    let date = Date(timeIntervalSince1970: 0)
    let name = SessionLog.fileName(kind: "英语", date: date)
    #expect(name.hasSuffix("_英语.xlsx"))
    #expect(name.range(of: #"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_"#, options: .regularExpression) != nil)
}

/// A package with a hand-written sheet and shared strings, for reader edge cases.
private func package(sheet: String, sharedStrings: String? = nil, prefix: String = "") -> Data {
    let ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
    let open = prefix.isEmpty ? "<worksheet xmlns=\"\(ns)\">" : "<\(prefix)worksheet xmlns:\(prefix.dropLast())=\"\(ns)\">"
    let close = "</\(prefix)worksheet>"
    var entries: [(name: String, data: Data)] = [("xl/worksheets/sheet1.xml", Data((open + sheet + close).utf8))]
    if let sharedStrings {
        entries.append(("xl/sharedStrings.xml", Data("<sst xmlns=\"\(ns)\">\(sharedStrings)</sst>".utf8)))
    }
    return ZipArchive.write(entries)
}

@Test func readerHandlesRichTextPhoneticsTypesAndGaps() throws {
    let shared = "<si><r><t>富</t></r><r><t xml:space=\"preserve\">文本 </t></r><rPh sb=\"0\" eb=\"1\"><t>ふ</t></rPh></si><si><t>二</t></si>"
    let sheet = """
        <sheetData><row r="1"><c r="A1" t="s"><v>0</v></c><c r="C1" t="b"><v>1</v></c></row>\
        <row r="3"><c r="B3" t="str"><v>公式结果</v></c><c t="s"><v>1</v></c><c r="E3"><v>3.5</v></c></row></sheetData>
        """
    let rows = try XLSX.readFirstSheet(package(sheet: sheet, sharedStrings: shared))
    #expect(rows == [["富文本 ", "", "TRUE"], [], ["", "公式结果", "二", "", "3.5"]])
}

@Test func readerAcceptsNamespacePrefixes() throws {
    let sheet = "<x:sheetData><x:row r=\"1\"><x:c r=\"A1\" t=\"inlineStr\"><x:is><x:t>自定义</x:t></x:is></x:c></x:row></x:sheetData>"
    #expect(try XLSX.readFirstSheet(package(sheet: sheet, prefix: "x:")) == [["自定义"]])
}

@Test func readerRejectsOutOfRangeReferencesWithoutCrashing() {
    let longRef = "<sheetData><row r=\"1\"><c r=\"AAAAAAAAAAAAAAAAAAAA1\"><v>1</v></c></row></sheetData>"
    #expect(throws: (any Error).self) { try XLSX.readFirstSheet(package(sheet: longRef)) }
    let farColumn = "<sheetData><row r=\"1\"><c r=\"XFE1\"><v>1</v></c></row></sheetData>"
    #expect(throws: (any Error).self) { try XLSX.readFirstSheet(package(sheet: farColumn)) }
    let farRow = "<sheetData><row r=\"99999999\"><c><v>1</v></c></row></sheetData>"
    #expect(throws: (any Error).self) { try XLSX.readFirstSheet(package(sheet: farRow)) }
}

@Test func zipRejectsCorruptEntriesAndSkipsUnwantedOnes() throws {
    var archive = ZipArchive.write([("a.xml", Data("hello world".utf8)), ("media/big.png", Data([1, 2, 3]))])
    // Flip one payload byte of a.xml: the CRC check must catch it.
    let payloadOffset = 30 + "a.xml".utf8.count
    archive[payloadOffset] ^= 0xFF
    #expect(throws: ZipError.self) { try ZipArchive.read(archive) }
    let onlyMedia = try ZipArchive.read(archive) { $0.hasPrefix("media/") }
    #expect(onlyMedia == ["media/big.png": Data([1, 2, 3])])
}

@Test func zipReadsStreamedArchivesWithDataDescriptors() throws {
    // `zip - -` streams from stdin, which writes sizes in a trailing data descriptor (flag bit 3).
    let zip = Process()
    zip.executableURL = URL(fileURLWithPath: "/usr/bin/zip")
    zip.arguments = ["-q", "-", "-"]
    let input = Pipe(), output = Pipe()
    zip.standardInput = input
    zip.standardOutput = output
    try zip.run()
    let text = Data(String(repeating: "ToastFish 摸鱼 ", count: 200).utf8)
    input.fileHandleForWriting.write(text)
    try input.fileHandleForWriting.close()
    let archive = output.fileHandleForReading.readDataToEndOfFile()
    zip.waitUntilExit()
    #expect(zip.terminationStatus == 0)
    #expect(try ZipArchive.read(archive) == ["-": text])
}
