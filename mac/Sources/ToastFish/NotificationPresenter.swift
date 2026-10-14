import AVFoundation
import ToastFishCore

/// Bridges study sessions to macOS notifications and speech.
@MainActor
final class NotificationPresenter: StudyPresenter {
    private let notifications: NotificationService
    private let synthesizer = AVSpeechSynthesizer()
    private var player: AVAudioPlayer?

    init(notifications: NotificationService) {
        self.notifications = notifications
    }

    func ask(_ prompt: StudyPrompt) async -> String? {
        await notifications.ask(prompt)
    }

    func show(_ message: String) {
        notifications.post(title: "ToastFish", body: message)
    }

    func pause(seconds: Double) async {
        try? await Task.sleep(for: .seconds(seconds))
    }

    /// Bumped on every pronounce so a slow download never plays over a newer word.
    private var pronounceGeneration = 0

    /// Youdao recording (cached in ~/Library/Caches/ToastFish/Mp3Cache), falling back to system TTS.
    func pronounce(_ word: Word, engType: Int) {
        pronounceGeneration += 1
        let generation = pronounceGeneration
        let headWord = word.headWord
        guard let cacheDir = Self.cacheDirectory else {
            speakEnglish(headWord, engType: engType)
            return
        }
        let file = cacheDir.appendingPathComponent(Pronunciation.cacheFileName(headWord: headWord, engType: engType))
        if FileManager.default.fileExists(atPath: file.path) {
            playFile(file, orSpeak: headWord, engType: engType)
            return
        }
        guard let url = Pronunciation.remoteURL(headWord: headWord, engType: engType) else {
            speakEnglish(headWord, engType: engType)
            return
        }
        Task { @MainActor in
            var request = URLRequest(url: url)
            request.timeoutInterval = 5
            let saved: Bool
            if let (data, response) = try? await URLSession.shared.data(for: request),
               (response as? HTTPURLResponse)?.statusCode == 200,
               Pronunciation.looksLikeAudio(data, mimeType: response.mimeType) {
                saved = (try? data.write(to: file, options: .atomic)) != nil
            } else {
                saved = false
            }
            guard generation == self.pronounceGeneration else { return }
            if saved {
                self.playFile(file, orSpeak: headWord, engType: engType)
            } else {
                self.speakEnglish(headWord, engType: engType)
            }
        }
    }

    private static let cacheDirectory: URL? = {
        guard let caches = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask).first else { return nil }
        let dir = caches.appendingPathComponent("ToastFish/Mp3Cache", isDirectory: true)
        return (try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)) != nil ? dir : nil
    }()

    private func playFile(_ url: URL, orSpeak text: String, engType: Int) {
        synthesizer.stopSpeaking(at: .immediate)
        player?.stop()
        guard let player = try? AVAudioPlayer(contentsOf: url), player.play() else {
            // A corrupt cache entry: drop it so the next try downloads again.
            try? FileManager.default.removeItem(at: url)
            speakEnglish(text, engType: engType)
            return
        }
        self.player = player
    }

    private func speakEnglish(_ text: String, engType: Int) {
        let utterance = AVSpeechUtterance(string: text)
        utterance.voice = AVSpeechSynthesisVoice(language: engType == 1 ? "en-US" : "en-GB")
        player?.stop()
        synthesizer.stopSpeaking(at: .immediate)
        synthesizer.speak(utterance)
    }

    func speakJapanese(_ text: String) {
        pronounceGeneration += 1  // cancels a pending English download's playback
        let utterance = AVSpeechUtterance(string: text)
        utterance.voice = AVSpeechSynthesisVoice(language: "ja-JP")
        player?.stop()
        synthesizer.stopSpeaking(at: .immediate)
        synthesizer.speak(utterance)
    }

    func playGoin(_ romaji: String) {
        // The bundled recordings use Kunrei spellings for these three kana.
        let file = ["chi": "ci", "tsu": "cu", "fu": "hu"][romaji] ?? romaji
        pronounceGeneration += 1
        guard let url = Bundle.main.url(forResource: file, withExtension: "mp3", subdirectory: "Goin") else { return }
        synthesizer.stopSpeaking(at: .immediate)
        player?.stop()
        player = try? AVAudioPlayer(contentsOf: url)
        player?.play()
    }

    /// Whether a Japanese voice is installed (the original warned when it wasn't).
    static var hasJapaneseVoice: Bool {
        AVSpeechSynthesisVoice.speechVoices().contains { $0.language == "ja-JP" }
    }
}
