import Foundation

/// One button on a study notification. `id` is what `StudyPresenter.ask` returns when it is chosen.
public struct PromptAction: Sendable, Equatable {
    public let id: String
    public let title: String

    public init(id: String, title: String) {
        self.id = id
        self.title = title
    }
}

/// A notification that waits for the user to pick one of its actions.
public struct StudyPrompt: Sendable, Equatable {
    public var title: String
    public var body: String
    public var actions: [PromptAction]

    public init(title: String, body: String, actions: [PromptAction]) {
        self.title = title
        self.body = body
        self.actions = actions
    }
}

/// Clicking the notification body instead of a button.
public let defaultActionID = "default"

/// The UI side of a study session (notifications in the app, a script in tests).
@MainActor
public protocol StudyPresenter: AnyObject {
    /// Shows the prompt and waits for an action id, or `defaultActionID` for a body click.
    /// Returns nil when the session task is cancelled.
    func ask(_ prompt: StudyPrompt) async -> String?
    /// Shows a plain notification without waiting.
    func show(_ message: String)
    /// Waits like the original Thread.Sleep between quiz notifications.
    func pause(seconds: Double) async
    func pronounce(_ word: Word, engType: Int)
    /// Reads Japanese text with a ja-JP voice.
    func speakJapanese(_ text: String)
    /// Plays the bundled Goin/<romaji>.mp3 recording.
    func playGoin(_ romaji: String)
}
