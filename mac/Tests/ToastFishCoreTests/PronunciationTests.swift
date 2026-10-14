import Foundation
import Testing
@testable import ToastFishCore

@Test func cacheNamesMatchOriginalAndAreSafe() {
    #expect(Pronunciation.cacheFileName(headWord: "absorb", engType: 1) == "absorb_us.mp3")
    #expect(Pronunciation.cacheFileName(headWord: "absorb", engType: 2) == "absorb_uk.mp3")
    #expect(Pronunciation.cacheFileName(headWord: "and/or", engType: 2) == "and_or_uk.mp3")
}

@Test func remoteURLEncodesPhrases() throws {
    let url = try #require(Pronunciation.remoteURL(headWord: "give up", engType: 1))
    #expect(url.absoluteString == "https://dict.youdao.com/dictvoice?audio=give%20up&type=1")
    let uk = try #require(Pronunciation.remoteURL(headWord: "café&co", engType: 2))
    let items = try #require(URLComponents(url: uk, resolvingAgainstBaseURL: false)?.queryItems)
    #expect(items == [URLQueryItem(name: "audio", value: "café&co"), URLQueryItem(name: "type", value: "2")])
}

@Test func onlyAudioPayloadsAreCached() {
    let id3 = Data("ID3".utf8) + Data(repeating: 0, count: 1000)
    let frame = Data([0xFF, 0xFB]) + Data(repeating: 0, count: 1000)
    let html = Data("<html>".utf8) + Data(repeating: 0x20, count: 1000)
    #expect(Pronunciation.looksLikeAudio(id3, mimeType: nil))
    #expect(Pronunciation.looksLikeAudio(frame, mimeType: "application/octet-stream"))
    #expect(Pronunciation.looksLikeAudio(html, mimeType: "audio/mpeg"))
    #expect(!Pronunciation.looksLikeAudio(html, mimeType: "text/html"))
    #expect(!Pronunciation.looksLikeAudio(Data("ID3".utf8), mimeType: "audio/mpeg"))
}
