import Carbon
import Foundation

/// Global hotkeys through Carbon's RegisterEventHotKey, which needs no accessibility permission.
@MainActor
final class HotKeys {
    enum Key: UInt32 {
        case q = 12, grave = 50, one = 18, two = 19, three = 20, four = 21
    }

    private static var handlers: [UInt32: () -> Void] = [:]
    private static var handlerInstalled = false
    private var refs: [Key: EventHotKeyRef] = [:]

    init() {
        Self.installHandlerIfNeeded()
    }

    /// Registers ⌥+key. Returns false when another app already owns the combination.
    @discardableResult
    func register(_ key: Key, action: @escaping () -> Void) -> Bool {
        guard refs[key] == nil else { return true }
        var ref: EventHotKeyRef?
        let id = EventHotKeyID(signature: OSType(0x5446_5348), id: key.rawValue)  // 'TFSH'
        let status = RegisterEventHotKey(key.rawValue, UInt32(optionKey), id, GetApplicationEventTarget(), 0, &ref)
        guard status == noErr, let ref else { return false }
        refs[key] = ref
        Self.handlers[key.rawValue] = action
        return true
    }

    func unregister(_ key: Key) {
        guard let ref = refs.removeValue(forKey: key) else { return }
        UnregisterEventHotKey(ref)
        Self.handlers[key.rawValue] = nil
    }

    private static func installHandlerIfNeeded() {
        guard !handlerInstalled else { return }
        handlerInstalled = true
        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(GetApplicationEventTarget(), { _, event, _ in
            var id = EventHotKeyID()
            GetEventParameter(event, EventParamName(kEventParamDirectObject), EventParamType(typeEventHotKeyID),
                              nil, MemoryLayout<EventHotKeyID>.size, nil, &id)
            let key = id.id
            // Carbon delivers hotkey events on the main thread's event loop.
            MainActor.assumeIsolated { HotKeys.handlers[key]?() }
            return noErr
        }, 1, &spec, nil, nil)
    }
}
