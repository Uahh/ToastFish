import Foundation
import SQLite3
import Testing
@testable import ToastFishCore

private let bundledDatabase = URL(fileURLWithPath: #filePath)
    .deletingLastPathComponent().deletingLastPathComponent()
    .deletingLastPathComponent().deletingLastPathComponent()
    .appendingPathComponent("Resources/inami.db")

private func withDatabase(_ body: (Database, URL) throws -> Void) throws {
    let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    defer { try? FileManager.default.removeItem(at: directory) }
    let url = try Database.prepareUserDatabase(bundledURL: bundledDatabase, appSupportDir: directory)
    try body(Database(url: url), url)
}

// Only used to set up legacy/corrupt schemas and force transactional failure on temporary copies.
private func fixtureSQL(_ sql: String, at url: URL) throws {
    var handle: OpaquePointer?
    #expect(sqlite3_open(url.path, &handle) == SQLITE_OK)
    defer { sqlite3_close(handle) }
    #expect(sqlite3_exec(handle, sql, nil, nil, nil) == SQLITE_OK)
}

@Test func configAndAppSettingsRoundTrip() throws {
    try withDatabase { db, url in
        var config = try db.loadGlobalConfig()
        #expect(config.currentBookName == "CET4_1")
        config.currentWordNumber = 25
        config.currentBookName = "StdJp_Mid"
        config.engType = 1
        config.autoPlay = false
        config.autoLog = false
        try db.updateGlobalConfig(config)
        #expect(try Database(url: url).loadGlobalConfig() == config)
        try db.updateBookName("Goin")
        try db.updateNumber(13)
        let settings = try AppSettings(database: db)
        #expect(settings.book == "Goin" && settings.wordNumber == 13)
        #expect(settings.engType == 1 && !settings.autoPlay && !settings.autoLog)
        settings.book = "GRE_2"
        settings.wordNumber = 12
        settings.engType = 2
        settings.autoPlay = true
        settings.autoLog = true
        try settings.save(to: db)
        let loaded = try AppSettings(database: Database(url: url))
        #expect(loaded.book == "GRE_2" && loaded.wordNumber == 12)
        #expect(loaded.engType == 2 && loaded.autoPlay && loaded.autoLog)
    }
}

@Test func countsLearningAndResetAcrossLanguages() throws {
    try withDatabase { db, _ in
        #expect(try db.selectCount(book: "CET4_1").total == 1065)
        for book in ["CET4_1", "StdJp_Mid", "Goin"] {
            try db.resetTableCount(book: book)
            try db.markWordLearned(book: book, wordRank: 2)
            try db.markWordLearned(book: book, wordRank: 3)
            try db.updateTableCount(book: book)
            #expect(try db.selectCount(book: book).current == 2)
            try db.resetTableCount(book: book)
            #expect(try db.selectCount(book: book).current == 0)
        }
        #expect(try db.allWords(book: "CET4_1").allSatisfy { $0.status == 0 })
        #expect(try db.allJpWords().allSatisfy { $0.status == 0 })
        #expect(try db.allGoinWords().count == 104)
        #expect(try db.allGoinWords().allSatisfy { $0.status == 0 })
        #expect(try db.goinProgress() == 0)
    }
}

@Test func unknownConfiguredBookFallsBackAndPersists() throws {
    try withDatabase { db, url in
        var expected = try db.loadGlobalConfig()
        expected.currentWordNumber = 27
        expected.autoPlay = false
        try db.updateGlobalConfig(expected)
        try fixtureSQL("UPDATE Global SET currentBookName='removed_book'", at: url)
        #expect(try db.loadGlobalConfig() == expected)
        // A fresh connection must read the persisted fallback without repairing it again.
        try fixtureSQL("CREATE TRIGGER reject_config_update BEFORE UPDATE ON Global BEGIN SELECT RAISE(ABORT, 'already repaired'); END;", at: url)
        #expect(try Database(url: url).loadGlobalConfig() == expected)
    }
}

@Test func randomSelectionIsUniqueAndOnlyUnlearned() throws {
    try withDatabase { db, url in
        let words = try db.randomWords(book: "CET4_1", n: 40)
        #expect(words.count == 40)
        #expect(Set(words.map(\.wordRank)).count == 40)
        #expect(words.allSatisfy { $0.status == 0 })
        #expect(try db.randomWords(book: "CET4_1", n: -1).isEmpty)
        #expect(try db.randomWords(book: "CET4_1", n: 5000).count == 1065)
        let japanese = try db.randomJpWords(n: 30)
        #expect(japanese.count == 30 && Set(japanese.map(\.wordRank)).count == 30)
        try fixtureSQL("UPDATE CET4_1 SET status=1 WHERE wordRank != 1001; UPDATE StdJp_Mid SET status='1' WHERE wordRank != 3000;", at: url)
        #expect(try db.randomWords(book: "CET4_1", n: 10).map(\.wordRank) == [1001])
        #expect(try db.randomJpWords(n: 10).map(\.wordRank) == [3000])
        try db.markWordLearned(book: "CET4_1", wordRank: 1001)
        try db.markWordLearned(book: "StdJp_Mid", wordRank: 3000)
        #expect(try db.randomWords(book: "CET4_1", n: 10).isEmpty)
        #expect(try db.randomJpWords(n: 10).isEmpty)
    }
}

@Test func cardPersistenceAndRollback() throws {
    try withDatabase { db, url in
        var cards = try db.allWords(book: "CET4_1").prefix(2).map { Card(word: $0) }
        for i in cards.indices {
            cards[i].status = .reviewed
            cards[i].difficulty = 0.72
            cards[i].daysBetweenReviews = 9.25
            cards[i].lastScore = 0.8
            cards[i].dateLastReviewed = Date(timeIntervalSince1970: 1_700_000_000)
        }
        try db.updateCards(cards)
        let saved = try Database(url: url).allWords(book: "CET4_1")
        for word in saved.prefix(2) {
            #expect(word.status == 5 && word.difficulty == 0.72)
            #expect(word.daysBetweenReviews == 9.25 && word.lastScore == 0.8)
            #expect(word.dateLastReviewed == "2023-11-14T22:13:20.000Z")
            #expect(Card(word: word).dateLastReviewed == cards[0].dateLastReviewed)
        }
        try db.updateTableCount(book: "CET4_1")
        #expect(try db.selectCount(book: "CET4_1").current == 2)
        try fixtureSQL("CREATE TRIGGER fail_card BEFORE UPDATE ON CET4_1 WHEN NEW.wordRank=3 BEGIN SELECT RAISE(ABORT, 'test rollback'); END;", at: url)
        cards[0].difficulty = 0.1
        cards[1].difficulty = 0.1
        #expect(throws: (any Error).self) { try db.updateCards(cards) }
        #expect(try db.allWords(book: "CET4_1")[0].difficulty == 0.72)
        try fixtureSQL("DROP TRIGGER fail_card", at: url)
        try db.resetTableCount(book: "CET4_1")
        let reset = try db.allWords(book: "CET4_1")[0]
        #expect(reset.status == 0 && reset.difficulty == 0.72) // C# only resets status.
    }
}

@Test func cardSelectionMatchesCSharpPoolsAndOverdueOrdering() throws {
    try withDatabase { db, url in
        try fixtureSQL("UPDATE CET4_1 SET status=5, lastScore=0.8, dateLastReviewed='2023-11-14T22:13:20Z'; UPDATE CET4_1 SET status=4 WHERE wordRank=2; UPDATE CET4_1 SET status=0 WHERE wordRank=3; UPDATE CET4_1 SET dateLastReviewed='2023-11-04T22:13:20Z' WHERE wordRank=4; UPDATE CET4_1 SET lastScore=0.4 WHERE wordRank=5;", at: url)
        let newCards = try db.generateRandomNewCardList(book: "CET4_1", max: 10)
        #expect(Set(newCards.map { $0.word.wordRank }) == [2, 3])
        let now = Date(timeIntervalSince1970: 1_700_000_000)
        let reviews = try db.overdueReviewedCards(book: "CET4_1", max: 3, now: now)
        #expect(reviews.count == 3)
        #expect(reviews.prefix(2).map { $0.word.wordRank } == [4, 5])
        #expect(reviews[2].percentOverdue(now: now) == 0) // Not-yet-due cards remain eligible.
        var graduated = try #require(newCards.first { $0.word.wordRank == 2 })
        graduated.status = .reviewed
        graduated.lastScore = 0.8
        graduated.dateLastReviewed = now.addingTimeInterval(-10 * 86_400)
        var lapsed = reviews[0]
        lapsed.status = .relearnStep1
        try db.updateCards([graduated, lapsed])

        // Each call starts a new session from the latest persisted card state.
        let nextNew = try db.generateRandomNewCardList(book: "CET4_1", max: 10)
        #expect(Set(nextNew.map { $0.word.wordRank }) == [3, 4])
        #expect(nextNew.first { $0.word.wordRank == 4 }?.status == .relearnStep1)
        let next = try db.overdueReviewedCards(book: "CET4_1", max: 2000, now: now)
        #expect(next.count == 1063)
        #expect(next.prefix(2).map { $0.word.wordRank } == [2, 5])
        #expect(!next.contains { $0.word.wordRank == 4 })
    }
}

@Test func prepareNeverOverwritesUserProgress() throws {
    try withDatabase { db, url in
        try db.updateNumber(77)
        let copied = try Database.prepareUserDatabase(bundledURL: bundledDatabase, appSupportDir: url.deletingLastPathComponent())
        #expect(copied == url)
        #expect(try Database(url: copied).loadGlobalConfig().currentWordNumber == 77)
        // An existing database is usable even if a later bundle no longer contains the source.
        _ = try Database.prepareUserDatabase(bundledURL: url.appendingPathComponent("missing"), appSupportDir: url.deletingLastPathComponent())
    }
}

@Test func rejectsUntrustedOrWrongLanguageTables() throws {
    try withDatabase { db, _ in
        let invalid = "CET4_1; DROP TABLE Global; --"
        #expect(throws: (any Error).self) { try db.allWords(book: invalid) }
        #expect(throws: (any Error).self) { try db.updateBookName(invalid) }
        #expect(throws: (any Error).self) { try db.selectCount(book: invalid) }
        #expect(throws: (any Error).self) { try db.resetTableCount(book: invalid) }
        #expect(throws: (any Error).self) { try db.updateTableCount(book: invalid) }
        #expect(throws: (any Error).self) { try db.markWordLearned(book: invalid, wordRank: 1) }
        #expect(throws: (any Error).self) { try db.allWords(book: "Goin") }
        let config = try db.loadGlobalConfig()
        #expect(config.currentBookName == "CET4_1")
    }
}

@Test func legacyColumnsAreAddedAndAllCatalogModelsDecode() throws {
    try withDatabase { db, url in
        try fixtureSQL("ALTER TABLE Global DROP COLUMN EngType; ALTER TABLE Global DROP COLUMN autoLog; ALTER TABLE CET4_1 DROP COLUMN difficulty; ALTER TABLE CET4_1 DROP COLUMN daysBetweenReviews; ALTER TABLE CET4_1 DROP COLUMN lastScore; ALTER TABLE CET4_1 DROP COLUMN dateLastReviewed;", at: url)
        let config = try db.loadGlobalConfig()
        #expect(config.engType == 2 && config.autoLog)
        for book in Books.catalog where book.language == .english {
            let words = try db.allWords(book: book.tableName)
            #expect(words.count == (try db.selectCount(book: book.tableName).total))
            #expect(words.allSatisfy { $0.bookID == book.tableName && !$0.headWord.isEmpty })
        }
        let first = try db.allWords(book: "CET4_1")[0]
        #expect(first.difficulty == 0.3 && first.daysBetweenReviews == 3 && first.lastScore == 0)
        #expect(first.dateLastReviewed.isEmpty)
        let japanese = try #require(db.allJpWords().first)
        #expect(japanese.headWord == "出会い" && japanese.hiragana == "であい" && japanese.phone == 0)
        let goin = try #require(db.allGoinWords().first)
        #expect(goin.wordRank == 1 && !goin.romaji.isEmpty && !goin.hiragana.isEmpty && !goin.katakana.isEmpty)
    }
}
