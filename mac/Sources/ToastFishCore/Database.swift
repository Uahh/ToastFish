import Foundation
import SQLite3

public enum DatabaseError: Error, LocalizedError {
    case sqlite(String)
    case invalidBook(String)
    case missingRow(String)

    public var errorDescription: String? {
        switch self {
        case .sqlite(let message): "数据库错误：\(message)"
        case .invalidBook(let name): "无效词库：\(name)"
        case .missingRow(let table): "数据库缺少记录：\(table)"
        }
    }
}

/// Synchronous connection. Keep on one actor/thread; not Sendable.
public final class Database {
    private let handle: OpaquePointer

    public init(url: URL) throws {
        var connection: OpaquePointer?
        let result = sqlite3_open_v2(url.path, &connection, SQLITE_OPEN_READWRITE, nil)
        guard result == SQLITE_OK, let connection else {
            let message = connection.map { String(cString: sqlite3_errmsg($0)) } ?? "无法打开数据库"
            if let connection { sqlite3_close(connection) }
            throw DatabaseError.sqlite(message)
        }
        handle = connection
        sqlite3_busy_timeout(handle, 5_000)
    }

    deinit { sqlite3_close(handle) }

    public static func prepareUserDatabase(bundledURL: URL, appSupportDir: URL) throws -> URL {
        let manager = FileManager.default
        try manager.createDirectory(at: appSupportDir, withIntermediateDirectories: true)
        let destination = appSupportDir.appendingPathComponent("inami.db")
        if !manager.fileExists(atPath: destination.path) {
            // copyItem never replaces an existing destination, including a concurrent first launch.
            do {
                try manager.copyItem(at: bundledURL, to: destination)
            } catch CocoaError.fileWriteFileExists {
                // Another process has already prepared the user database.
            }
        }
        return destination
    }

    public func loadGlobalConfig() throws -> GlobalConfig {
        let columns = try columnNames(in: "Global")
        if !columns.contains("engtype") {
            try execute("ALTER TABLE Global ADD COLUMN EngType INTEGER NOT NULL DEFAULT 2")
        }
        if !columns.contains("autolog") {
            try execute("ALTER TABLE Global ADD COLUMN autoLog INTEGER NOT NULL DEFAULT 1")
        }
        guard var config = try query("SELECT * FROM Global LIMIT 1", map: { row in
            GlobalConfig(currentWordNumber: row.int("currentWordNumber"),
                         currentBookName: row.text("currentBookName"),
                         autoPlay: row.int("autoPlay") != 0,
                         engType: row.int("EngType"), autoLog: row.int("autoLog") != 0)
        }).first else { throw DatabaseError.missingRow("Global") }
        if !Books.catalog.contains(where: { $0.tableName == config.currentBookName }) {
            config.currentBookName = "CET4_1"
            try updateBookName(config.currentBookName)
        }
        return config
    }

    public func updateGlobalConfig(_ config: GlobalConfig) throws {
        _ = try table(config.currentBookName)
        try execute("""
            UPDATE Global SET currentWordNumber=?, currentBookName=?, autoPlay=?, EngType=?, autoLog=?
            """, [.int(config.currentWordNumber), .text(config.currentBookName),
                  .int(config.autoPlay ? 1 : 0), .int(config.engType), .int(config.autoLog ? 1 : 0)])
    }

    public func updateBookName(_ book: String) throws {
        _ = try table(book)
        try execute("UPDATE Global SET currentBookName=?", [.text(book)])
    }

    public func updateNumber(_ number: Int) throws {
        try execute("UPDATE Global SET currentWordNumber=?", [.int(number)])
    }

    public func selectCount(book: String) throws -> (current: Int, total: Int) {
        _ = try table(book)
        guard let count = try query("SELECT * FROM Count WHERE bookName=?", [.text(book)], map: {
            BookCount(bookName: $0.text("bookName"), number: $0.int("number"), current: $0.int("current"))
        }).first else { throw DatabaseError.missingRow("Count: \(book)") }
        return (count.current, count.number)
    }

    public func updateTableCount(book: String) throws {
        let name = try table(book)
        try execute("UPDATE Count SET current=(SELECT COUNT(*) FROM \(name) WHERE CAST(status AS INTEGER) != ?) WHERE bookName=?",
                    [.int(0), .text(book)])
    }

    /// C# resets only status, preserving difficulty, intervals, scores, and review dates.
    /// Recount atomically so the menu immediately shows zero.
    public func resetTableCount(book: String) throws {
        let name = try table(book)
        try transaction {
            try execute("UPDATE \(name) SET status=?", [.int(0)])
            try updateTableCount(book: book)
        }
    }

    public func markWordLearned(book: String, wordRank: Int) throws {
        let name = try table(book)
        try execute("UPDATE \(name) SET status=? WHERE wordRank=?", [.int(1), .int(wordRank)])
    }

    public func allWords(book: String) throws -> [Word] {
        let name = try table(book, englishOnly: true)
        try ensureReviewColumns(in: name)
        return try query("SELECT * FROM \(name) ORDER BY wordRank", map: Self.word)
    }

    public func allJpWords() throws -> [JpWord] {
        try query("SELECT * FROM StdJp_Mid ORDER BY wordRank") { row in
            JpWord(wordRank: row.int("wordRank"), bookID: row.text("bookID"), status: row.int("status"),
                   headWord: row.text("headWord"), phone: row.int("phone"), tranCN: row.text("tranCN"),
                   hiragana: row.text("hiragana"), pos: row.text("pos"))
        }
    }

    public func allGoinWords() throws -> [GoinWord] {
        try query("SELECT * FROM Goin ORDER BY wordRank") { row in
            GoinWord(wordRank: row.int("wordRank"), bookID: row.text("bookID"), status: row.int("status"),
                     hiragana: row.text("hiragana"), romaji: row.text("romaji"), katakana: row.text("katakana"))
        }
    }

    public func goinProgress() throws -> Int { try selectCount(book: "Goin").current }

    /// Goin's Count.current is the 1-based position of the next kana to learn (C# UpdateCount, cyclic over 104).
    public func setGoinProgress(_ position: Int) throws {
        try execute("UPDATE Count SET current=? WHERE bookName='Goin'", [.int(position)])
    }

    /// Like GetRandomWordList: cap at the number of unseen words; never fill with learned words.
    public func randomWords(book: String, n: Int) throws -> [Word] {
        Array(try allWords(book: book).filter { $0.status == 0 }.shuffled().prefix(max(0, n)))
    }

    public func randomJpWords(n: Int) throws -> [JpWord] {
        // Select from the filtered array, correcting the original Japanese rank/index mismatch.
        Array(try allJpWords().filter { $0.status == 0 }.shuffled().prefix(max(0, n)))
    }

    /// Called once per study session, like C#'s fresh Select for each RecitationSM2.
    /// Rebuild from persisted state; non-reviewed cards include relearning cards.
    public func generateRandomNewCardList(book: String, max count: Int) throws -> [Card] {
        let pool = try allWords(book: book).filter { $0.status != CardStatus.reviewed.rawValue }
            .map { Card(word: $0) }.shuffled()
        return Array(pool.prefix(max(0, count)))
    }

    /// C# sorts ALL reviewed cards by overdue descending, even when overdue is below one.
    /// Rebuild on every session's call so saved reviews and lapses are reflected.
    public func overdueReviewedCards(book: String, max count: Int, now: Date = Date()) throws -> [Card] {
        let pool = try allWords(book: book).filter { $0.status == CardStatus.reviewed.rawValue }
            .map { Card(word: $0, now: now) }
            .sorted { $0.percentOverdue(now: now) > $1.percentOverdue(now: now) }
        return Array(pool.prefix(max(0, count)))
    }

    /// Each word's bookID identifies its source table. The entire batch commits or rolls back.
    public func updateCards(_ cards: [Card]) throws {
        try transaction {
            for book in Set(cards.map { $0.word.bookID }) {
                try ensureReviewColumns(in: table(book, englishOnly: true))
            }
            for card in cards {
                let name = try table(card.word.bookID, englishOnly: true)
                try execute("""
                    UPDATE \(name) SET status=?, difficulty=?, daysBetweenReviews=?, lastScore=?, dateLastReviewed=?
                    WHERE wordRank=?
                    """, [.int(card.status.rawValue), .real(card.difficulty), .real(card.daysBetweenReviews),
                          .real(card.lastScore), .text(ReviewDate.string(from: card.dateLastReviewed)), .int(card.word.wordRank)])
            }
        }
    }

    private func table(_ name: String, englishOnly: Bool = false) throws -> String {
        guard let book = Books.catalog.first(where: { $0.tableName == name }),
              !englishOnly || book.language == .english else { throw DatabaseError.invalidBook(name) }
        return book.tableName
    }

    private func columnNames(in validatedTable: String) throws -> Set<String> {
        Set(try query("PRAGMA table_info(\(validatedTable))") { $0.text("name").lowercased() })
    }

    private func ensureReviewColumns(in validatedTable: String) throws {
        let columns = try columnNames(in: validatedTable)
        // SQL DDL defaults are fixed schema literals, never interpolated user data.
        for (column, definition) in [
            ("difficulty", "REAL NOT NULL DEFAULT 0.3"),
            ("daysBetweenReviews", "REAL NOT NULL DEFAULT 3"),
            ("lastScore", "REAL NOT NULL DEFAULT 0"),
            ("dateLastReviewed", "TEXT DEFAULT NULL"),
        ] where !columns.contains(column.lowercased()) {
            try execute("ALTER TABLE \(validatedTable) ADD COLUMN \(column) \(definition)")
        }
    }

    private static func word(_ row: Row) -> Word {
        var word = Word(wordRank: row.int("wordRank"), bookID: row.text("bookID"), status: row.int("status"))
        word.headWord = row.text("headWord")
        word.usPhone = row.text("usphone")
        word.ukPhone = row.text("ukphone")
        word.usSpeech = row.text("usspeech")
        word.ukSpeech = row.text("ukspeech")
        word.tranCN = row.text("tranCN")
        word.pos = row.text("pos")
        word.tranOther = row.text("tranOther")
        word.question = row.text("question")
        word.explain = row.text("explain")
        word.rightIndex = row.optionalInt("rightIndex")
        word.examType = row.optionalInt("examType")
        word.choiceIndexOne = row.text("choiceIndexOne")
        word.choiceIndexTwo = row.text("choiceIndexTwo")
        word.choiceIndexThree = row.text("choiceIndexThree")
        word.choiceIndexFour = row.text("choiceIndexFour")
        word.sentence = row.text("sentence")
        word.sentenceCN = row.text("sentenceCN")
        word.phrase = row.text("phrase")
        word.phraseCN = row.text("phraseCN")
        word.difficulty = row.real("difficulty")
        word.daysBetweenReviews = row.real("daysBetweenReviews")
        word.lastScore = row.real("lastScore")
        word.dateLastReviewed = row.text("dateLastReviewed")
        return word
    }

    private enum Value { case int(Int), real(Double), text(String) }

    private struct Row {
        let statement: OpaquePointer
        let columns: [String: Int32]

        func text(_ name: String) -> String {
            guard let index = columns[name.lowercased()], let bytes = sqlite3_column_text(statement, index) else { return "" }
            return String(cString: bytes)
        }
        func optionalInt(_ name: String) -> Int? {
            guard let index = columns[name.lowercased()], sqlite3_column_type(statement, index) != SQLITE_NULL else { return nil }
            return Int(sqlite3_column_int64(statement, index))
        }
        func int(_ name: String) -> Int { optionalInt(name) ?? 0 }
        func real(_ name: String) -> Double {
            guard let index = columns[name.lowercased()] else { return 0 }
            return sqlite3_column_double(statement, index)
        }
    }

    private func query<T>(_ sql: String, _ values: [Value] = [], map: (Row) throws -> T) throws -> [T] {
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(handle, sql, -1, &statement, nil) == SQLITE_OK, let statement else {
            throw sqliteError()
        }
        defer { sqlite3_finalize(statement) }
        for (offset, value) in values.enumerated() {
            let index = Int32(offset + 1)
            let result: Int32
            switch value {
            case .int(let value): result = sqlite3_bind_int64(statement, index, Int64(value))
            case .real(let value): result = sqlite3_bind_double(statement, index, value)
            case .text(let value):
                result = value.withCString {
                    sqlite3_bind_text(statement, index, $0, -1, unsafeBitCast(-1, to: sqlite3_destructor_type.self))
                }
            }
            guard result == SQLITE_OK else { throw sqliteError() }
        }
        var columns: [String: Int32] = [:]
        for index in 0..<sqlite3_column_count(statement) {
            columns[String(cString: sqlite3_column_name(statement, index)).lowercased()] = index
        }
        let row = Row(statement: statement, columns: columns)
        var results: [T] = []
        while true {
            switch sqlite3_step(statement) {
            case SQLITE_ROW: results.append(try map(row))
            case SQLITE_DONE: return results
            default: throw sqliteError()
            }
        }
    }

    private func execute(_ sql: String, _ values: [Value] = []) throws {
        let _: [Void] = try query(sql, values) { _ in () }
    }

    private func transaction(_ body: () throws -> Void) throws {
        try execute("BEGIN IMMEDIATE TRANSACTION")
        do {
            try body()
            try execute("COMMIT")
        } catch {
            try? execute("ROLLBACK")
            throw error
        }
    }

    private func sqliteError() -> DatabaseError {
        .sqlite(String(cString: sqlite3_errmsg(handle)))
    }
}
