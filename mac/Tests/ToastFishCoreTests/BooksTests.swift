import Testing
@testable import ToastFishCore

@Test func catalogHasExpectedLanguagesAndUniqueTables() {
    #expect(Books.catalog.filter { $0.language == .english }.count == 15)
    #expect(Books.catalog.filter { $0.language == .japanese }.count == 2)
    #expect(Set(Books.catalog.map(\.tableName)).count == Books.catalog.count)
    #expect(Books.catalog.first?.tableName == "CET4_1")
}
