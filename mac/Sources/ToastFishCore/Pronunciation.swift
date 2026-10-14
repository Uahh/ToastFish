import Foundation

/// Youdao dictionary audio, as DownloadMp3.PlayMp3 used it: type=1 is American, type=2 British.
public enum Pronunciation {
    /// Cache file name, e.g. "absorb_us.mp3". Characters that can't appear in a file name are replaced.
    public static func cacheFileName(headWord: String, engType: Int) -> String {
        let safe = headWord.map { "/:\\\0".contains($0) ? "_" : String($0) }.joined()
        return "\(safe)_\(engType == 1 ? "us" : "uk").mp3"
    }

    public static func remoteURL(headWord: String, engType: Int) -> URL? {
        var components = URLComponents(string: "https://dict.youdao.com/dictvoice")
        components?.queryItems = [
            URLQueryItem(name: "audio", value: headWord),
            URLQueryItem(name: "type", value: engType == 1 ? "1" : "2"),
        ]
        return components?.url
    }

    /// Youdao answers unknown words with an HTML/JSON error page; only keep real audio.
    public static func looksLikeAudio(_ data: Data, mimeType: String?) -> Bool {
        guard data.count > 512 else { return false }
        if let mimeType, mimeType.hasPrefix("audio/") { return true }
        // MP3 files start with an ID3 tag or an MPEG frame sync.
        return data.starts(with: Array("ID3".utf8)) || (data[data.startIndex] == 0xFF && data[data.startIndex + 1] & 0xE0 == 0xE0)
    }
}
