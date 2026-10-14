import Foundation
import ToastFishCore
import UserNotifications

@MainActor
final class NotificationService: NSObject, UNUserNotificationCenterDelegate {
    private let center: UNUserNotificationCenter?
    /// The prompt waiting for an answer. Only one study prompt is on screen at a time.
    private var pending: (id: String, request: UNNotificationRequest, continuation: CheckedContinuation<String?, Never>,
                          actions: [PromptAction], reposted: Bool)?
    /// Set when a prompt could not be posted, so the caller can tell it apart from a cancel.
    private(set) var lastPostError: String?

    override init() {
        // Calling current() outside an app bundle can raise an Objective-C exception.
        if Bundle.main.bundleIdentifier != nil {
            center = UNUserNotificationCenter.current()
        } else {
            center = nil
        }
        super.init()
        center?.delegate = self
    }

    var isAvailable: Bool { center != nil }

    func requestAuthorization() {
        guard let center else {
            Self.log("未从应用包运行，通知将输出到 stderr。")
            return
        }
        center.requestAuthorization(options: [.alert, .sound]) { granted, error in
            if let error {
                Self.log("通知授权失败：\(error.localizedDescription)")
            } else if !granted {
                Self.log("通知权限未开启，请在系统设置中允许 ToastFish 通知。")
            }
        }
    }

    enum Permission { case allowed(alertStyle: Bool), denied }

    /// Asks for permission if it was never requested. Buttons only stay on screen with the 提醒 (alert) style.
    func ensurePermission() async -> Permission {
        guard let center else { return .denied }
        var settings = await center.notificationSettings()
        if settings.authorizationStatus == .notDetermined {
            _ = try? await center.requestAuthorization(options: [.alert, .sound])
            settings = await center.notificationSettings()
        }
        switch settings.authorizationStatus {
        case .authorized, .provisional: return .allowed(alertStyle: settings.alertStyle == .alert)
        default: return .denied
        }
    }

    func post(title: String, body: String) {
        guard let center else {
            Self.log("\(title)：\(body)")
            return
        }
        let content = UNMutableNotificationContent()
        content.title = title
        content.body = body
        content.sound = .default
        let request = UNNotificationRequest(
            identifier: UUID().uuidString, content: content, trigger: nil
        )
        center.add(request) { error in
            if let error {
                Self.log("发送通知失败：\(error.localizedDescription)")
            }
        }
    }

    /// Shows a notification with one button per action and waits for the user's choice.
    /// Returns nil when the calling task is cancelled or notifications are unavailable.
    func ask(_ prompt: StudyPrompt) async -> String? {
        guard let center else {
            Self.log("\(prompt.title)：\(prompt.body)（需要从应用包运行才能作答）")
            return nil
        }
        resolvePending(with: nil)
        center.removeAllDeliveredNotifications()

        let id = UUID().uuidString
        // Action titles change per question, so every prompt gets its own category.
        let category = UNNotificationCategory(
            identifier: "prompt.\(id)",
            actions: prompt.actions.map { UNNotificationAction(identifier: $0.id, title: $0.title, options: []) },
            intentIdentifiers: [],
            options: [.customDismissAction]
        )
        center.setNotificationCategories([category])
        // Registration is asynchronous; reading it back makes sure the buttons exist before posting.
        _ = await center.notificationCategories()

        let content = UNMutableNotificationContent()
        content.title = prompt.title
        content.body = prompt.body
        content.categoryIdentifier = category.identifier
        let request = UNNotificationRequest(identifier: id, content: content, trigger: nil)
        lastPostError = nil

        return await withTaskCancellationHandler {
            await withCheckedContinuation { continuation in
                // Install the continuation before posting so an instant click can't be missed.
                pending = (id, request, continuation, prompt.actions, false)
                if Task.isCancelled {
                    resolvePending(with: nil)
                    return
                }
                center.add(request) { error in
                    guard let error else { return }
                    let message = error.localizedDescription
                    Task { @MainActor in
                        Self.log("发送通知失败：\(message)")
                        // Ignore failures of a prompt that a newer one already replaced.
                        guard self.pending?.id == id else { return }
                        self.lastPostError = message
                        self.resolvePending(id: id, with: nil)
                    }
                }
            }
        } onCancel: {
            Task { @MainActor in self.resolvePending(id: id, with: nil) }
        }
    }

    /// ⌥1–⌥4: press the n-th button of the prompt on screen, like the Windows hotkeys.
    func answerPending(buttonIndex: Int) {
        guard let actions = pending?.actions, actions.indices.contains(buttonIndex) else { return }
        answerByHotKey(actions[buttonIndex].id)
    }

    /// ⌥`: the 发音 button, when the prompt has one.
    func answerPendingVoice() {
        guard let voice = pending?.actions.first(where: { $0.id == "voice" }) else { return }
        answerByHotKey(voice.id)
    }

    /// The notification stays on screen after a hotkey answer; remove it so its dead buttons don't linger.
    private func answerByHotKey(_ actionID: String) {
        guard let id = pending?.id else { return }
        center?.removeDeliveredNotifications(withIdentifiers: [id])
        resolvePending(with: actionID)
    }

    func clearDelivered() {
        center?.removeAllDeliveredNotifications()
    }

    private func resolvePending(id: String? = nil, with answer: String?) {
        guard let current = pending, id == nil || current.id == id else { return }
        pending = nil
        current.continuation.resume(returning: answer)
    }

    private func handleResponse(requestID: String, action: String) {
        guard let current = pending, current.id == requestID else { return }
        switch action {
        case UNNotificationDismissActionIdentifier:
            // Closing once by accident shows it again; closing it twice pauses until 开始！ is clicked.
            guard !current.reposted else { return }
            pending?.reposted = true
            center?.add(current.request)
        case UNNotificationDefaultActionIdentifier:
            resolvePending(with: defaultActionID)
        default:
            resolvePending(with: action)
        }
    }

    nonisolated func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        didReceive response: UNNotificationResponse,
        withCompletionHandler completionHandler: @escaping () -> Void
    ) {
        let requestID = response.notification.request.identifier
        let action = response.actionIdentifier
        Task { @MainActor in self.handleResponse(requestID: requestID, action: action) }
        completionHandler()
    }

    nonisolated func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        completionHandler([.banner, .list, .sound])
    }

    private nonisolated static func log(_ message: String) {
        FileHandle.standardError.write(Data("ToastFish: \(message)\n".utf8))
    }
}
