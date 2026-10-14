/// Owned by the app's main actor; this mutable holder is intentionally not Sendable.
public final class AppSettings {
    public var book: String
    public var wordNumber: Int
    public var engType: Int
    public var autoPlay: Bool
    public var autoLog: Bool

    public init(database: Database) throws {
        let config = try database.loadGlobalConfig()
        book = config.currentBookName
        wordNumber = config.currentWordNumber
        engType = config.engType
        autoPlay = config.autoPlay
        autoLog = config.autoLog
    }

    public func save(to database: Database) throws {
        try database.updateGlobalConfig(GlobalConfig(
            currentWordNumber: wordNumber, currentBookName: book,
            autoPlay: autoPlay, engType: engType, autoLog: autoLog
        ))
    }
}
