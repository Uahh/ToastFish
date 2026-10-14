public enum BookLanguage: Sendable {
    case english
    case japanese
}

public struct Book: Sendable {
    public let tableName: String
    public let displayName: String
    public let language: BookLanguage
}

public enum Books {
    public static let catalog: [Book] = [
        Book(tableName: "CET4_1", displayName: "四级核心词汇", language: .english),
        Book(tableName: "CET4_3", displayName: "四级完整词汇", language: .english),
        Book(tableName: "CET6_1", displayName: "六级核心词汇", language: .english),
        Book(tableName: "CET6_3", displayName: "六级完整词汇", language: .english),
        Book(tableName: "GMAT_3", displayName: "GMAT词汇", language: .english),
        Book(tableName: "GRE_2", displayName: "GRE词汇", language: .english),
        Book(tableName: "IELTS_3", displayName: "IELTS词汇", language: .english),
        Book(tableName: "TOEFL_2", displayName: "TOEFL词汇", language: .english),
        Book(tableName: "SAT_2", displayName: "SAT词汇", language: .english),
        Book(tableName: "KaoYan_1", displayName: "考研必考词汇", language: .english),
        Book(tableName: "KaoYan_2", displayName: "考研完整词汇", language: .english),
        Book(tableName: "Level4_1", displayName: "专四真题高频词", language: .english),
        Book(tableName: "Level4luan_2", displayName: "专四核心词汇", language: .english),
        Book(tableName: "Level8_1", displayName: "专八真题高频词", language: .english),
        Book(tableName: "Level8luan_2", displayName: "专八核心词汇", language: .english),
        Book(tableName: "Goin", displayName: "顺序五十音", language: .japanese),
        Book(tableName: "StdJp_Mid", displayName: "标准日本语中级词汇", language: .japanese),
    ]
}
