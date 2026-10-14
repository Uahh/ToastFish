import AppKit
import ServiceManagement
import ToastFishCore
import UniformTypeIdentifiers

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var statusItem: NSStatusItem?
    private var notifications: NotificationService?
    private var database: Database?
    private var settings: AppSettings?
    private var bookItems: [NSMenuItem] = []
    private var presenter: NotificationPresenter?
    private var studyTask: Task<Void, Never>?
    private var checkedAlertStyle = false
    private var hotKeys: HotKeys?
    private var sessionToken = 0
    private var launchItem: NSMenuItem?

    func applicationDidFinishLaunching(_ notification: Notification) {
        if let bundleID = Bundle.main.bundleIdentifier,
           NSRunningApplication.runningApplications(withBundleIdentifier: bundleID)
            .contains(where: { $0.processIdentifier != ProcessInfo.processInfo.processIdentifier }) {
            let alert = NSAlert()
            alert.messageText = "程序已经在运行了"
            alert.addButton(withTitle: "好")
            NSApp.activate(ignoringOtherApps: true)
            alert.runModal()
            NSApp.terminate(nil)
            return
        }

        do {
            guard let bundledURL = Bundle.main.url(forResource: "inami", withExtension: "db") else {
                throw DatabaseError.sqlite("找不到 inami.db，请使用打包后的应用。")
            }
            let support = try FileManager.default.url(
                for: .applicationSupportDirectory, in: .userDomainMask,
                appropriateFor: nil, create: true
            ).appendingPathComponent("ToastFish", isDirectory: true)
            let url = try Database.prepareUserDatabase(bundledURL: bundledURL, appSupportDir: support)
            let database = try Database(url: url)
            settings = try AppSettings(database: database)
            self.database = database
        } catch {
            showDatabaseError(error)
            NSApp.terminate(nil)
            return
        }

        let service = NotificationService()
        notifications = service
        presenter = NotificationPresenter(notifications: service)
        installStatusItem()
        let hotKeys = HotKeys()
        self.hotKeys = hotKeys
        if !hotKeys.register(.q, action: { [weak self] in self?.startStudy(nil) }) {
            notifications?.post(title: "ToastFish", body: "快捷键 ⌥Q 已被其他程序占用")
        }
        service.requestAuthorization()
        service.post(title: "ToastFish 已启动", body: "点菜单栏上的小鱼图标打开菜单，或按 ⌥Q 开始背单词。看不到图标时，再次打开 ToastFish.app 会弹出菜单。")
    }

    /// Opening the app again (Finder, Dock, Spotlight) shows the menu at the pointer. On Macs with a notch
    /// the status icon can be hidden when the menu bar is full, so this keeps the app reachable.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if let menu = statusItem?.menu {
            NSApp.activate(ignoringOtherApps: true)
            menu.popUp(positioning: nil, at: NSEvent.mouseLocation, in: nil)
        }
        return false
    }

    private func installStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        statusItem = item
        // A crisp monochrome glyph; the colour character icon turns into a blob as a template image.
        if let image = NSImage(systemSymbolName: "fish", accessibilityDescription: "ToastFish") {
            image.isTemplate = true
            item.button?.image = image
        } else {
            item.button?.title = "🐟"
        }
        item.button?.toolTip = "ToastFish"
        item.menu = makeMenu()
    }

    private func makeMenu() -> NSMenu {
        let menu = NSMenu()
        menu.addItem(actionItem("开始！", action: #selector(startStudy(_:))))
        menu.addItem(actionItem("导入单词", action: #selector(importWords(_:))))
        for (title, language) in [("英语词汇", BookLanguage.english), ("日语词汇", .japanese)] {
            let submenu = addSubmenu(title, to: menu)
            for book in Books.catalog where book.language == language {
                let item = actionItem(book.displayName, action: #selector(selectBook(_:)))
                item.representedObject = book.tableName
                item.state = book.tableName == self.settings?.book ? .on : .off
                submenu.addItem(item)
                bookItems.append(item)
            }
        }
        let tests = addSubmenu("随机测试", to: menu)
        tests.addItem(actionItem("随机单词测试", action: #selector(randomWordTest(_:))))
        tests.addItem(actionItem("随机五十音测试", action: #selector(randomGoinTest(_:))))
        tests.addItem(actionItem("随机日语单词测试", action: #selector(randomJpWordTest(_:))))
        let settings = addSubmenu("参数设置", to: menu)
        settings.addItem(actionItem("单词个数", action: #selector(chooseWordNumber(_:))))
        settings.addItem(actionItem("英标类型", action: #selector(chooseEngType(_:))))
        settings.addItem(toggleItem("自动播放", state: self.settings?.autoPlay == true ? .on : .off))
        settings.addItem(toggleItem("自动日志", state: self.settings?.autoLog == true ? .on : .off))
        settings.addItem(actionItem("重置进度", action: #selector(resetProgress(_:))))
        settings.addItem(.separator())
        settings.addItem(actionItem("打开背诵记录文件夹", action: #selector(openLogFolder(_:))))
        settings.addItem(actionItem("显示自定义模板", action: #selector(openTemplate(_:))))
        let help = addSubmenu("使用说明", to: menu)
        help.addItem(actionItem("快捷方式", action: #selector(showShortcuts(_:))))
        help.addItem(actionItem("使用说明(必读)", action: #selector(openInstructions(_:))))
        help.addItem(actionItem("官方网站", action: #selector(openWebsite(_:))))
        help.addItem(actionItem("Star!!", action: #selector(openStar(_:))))
        let launchItem = actionItem("开机启动", action: #selector(toggleLaunchAtLogin(_:)))
        self.launchItem = launchItem
        menu.addItem(launchItem)
        menu.delegate = self
        menu.addItem(actionItem("退出", action: #selector(quit(_:))))
        return menu
    }

    private func actionItem(_ title: String, action: Selector) -> NSMenuItem {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
        item.target = self
        return item
    }

    private func toggleItem(_ title: String, state: NSControl.StateValue = .on) -> NSMenuItem {
        let item = actionItem(title, action: #selector(toggle(_:)))
        item.state = state
        return item
    }

    private func addSubmenu(_ title: String, to menu: NSMenu) -> NSMenu {
        let item = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        let submenu = NSMenu(title: title)
        item.submenu = submenu
        menu.addItem(item)
        return submenu
    }

    @objc private func selectBook(_ sender: NSMenuItem) {
        guard let tableName = sender.representedObject as? String,
              let database, let settings else { return }
        do {
            // Goin's current count is a cyclic counter, not derived from row status.
            if tableName != "Goin" {
                try database.updateTableCount(book: tableName)
            }
            let count = try database.selectCount(book: tableName)
            try database.updateBookName(tableName)
            settings.book = tableName
            for item in bookItems {
                item.state = (item.representedObject as? String) == settings.book ? .on : .off
            }
            notifications?.post(title: "ToastFish", body: "当前词库：\(sender.title)\n当前进度：\(count.current)/\(count.total)")
            if tableName == "StdJp_Mid" && !NotificationPresenter.hasJapaneseVoice {
                showAlert("未检测到日语语音", info: "发音功能需要日语语音。请在 系统设置 → 辅助功能 → 朗读内容 → 系统声音 → 管理声音 中添加日语声音。")
            }
        } catch {
            showDatabaseError(error)
        }
    }

    // MARK: Study

    @objc private func startStudy(_ sender: NSMenuItem?) {
        guard let settings else { return }
        let number = settings.wordNumber
        switch settings.book {
        case "Goin":
            runSession { [weak self] database, presenter in
                let session = JapaneseStudySession(database: database, presenter: presenter)
                defer { self?.writeLog(WordListIO.export(session.studiedGoin), kind: "五十音", empty: session.studiedGoin.isEmpty) }
                return try await session.runOrderedGoin(number: number)
            }
        case "StdJp_Mid":
            runSession { [weak self] database, presenter in
                let session = JapaneseStudySession(database: database, presenter: presenter)
                defer { self?.writeLog(WordListIO.export(session.studiedWords), kind: "日语", empty: session.studiedWords.isEmpty) }
                return try await session.runRecitation(number: number)
            }
        default:
            let options = englishOptions(book: settings.book)
            runSession { [weak self] database, presenter in
                let session = EnglishStudySession(database: database, presenter: presenter, options: options)
                defer { self?.writeLog(WordListIO.export(session.studiedWords), kind: "英语", empty: session.studiedWords.isEmpty) }
                return try await session.runSM2()
            }
        }
    }

    @objc private func randomWordTest(_ sender: NSMenuItem) {
        guard let settings else { return }
        // Like the original, a Japanese selection falls back to GRE words for this test (without changing the selection).
        let isEnglish = Books.catalog.first { $0.tableName == settings.book }?.language == .english
        let options = englishOptions(book: isEnglish ? settings.book : "GRE_2")
        runSession { [weak self] database, presenter in
            let session = EnglishStudySession(database: database, presenter: presenter, options: options)
            defer { self?.writeLog(WordListIO.export(session.studiedWords), kind: "随机英语单词", empty: session.studiedWords.isEmpty) }
            return try await session.runRandomTest()
        }
    }

    @objc private func randomGoinTest(_ sender: NSMenuItem) {
        guard let number = settings?.wordNumber else { return }
        runSession { [weak self] database, presenter in
            let session = JapaneseStudySession(database: database, presenter: presenter)
            defer { self?.writeLog(WordListIO.export(session.studiedGoin), kind: "随机五十音", empty: session.studiedGoin.isEmpty) }
            return try await session.runRandomGoin(number: number)
        }
    }

    @objc private func randomJpWordTest(_ sender: NSMenuItem) {
        guard let number = settings?.wordNumber else { return }
        runSession { [weak self] database, presenter in
            let session = JapaneseStudySession(database: database, presenter: presenter)
            defer { self?.writeLog(WordListIO.export(session.studiedWords), kind: "随机日语单词", empty: session.studiedWords.isEmpty) }
            return try await session.runRandomTest(number: number)
        }
    }

    // MARK: Import and logs

    @objc private func importWords(_ sender: NSMenuItem) {
        let panel = NSOpenPanel()
        panel.title = "导入单词"
        panel.message = "选择背诵记录或按自定义模板填写的 Excel 文件（.xlsx）"
        panel.allowedContentTypes = [UTType(filenameExtension: "xlsx")].compactMap { $0 }
        panel.allowsMultipleSelection = false
        NSApp.activate(ignoringOtherApps: true)
        guard panel.runModal() == .OK, let url = panel.url else { return }  // cancel is silent, as in #111
        let imported: WordListIO.Imported
        do {
            imported = try WordListIO.importFile(Data(contentsOf: url))
        } catch {
            showAlert("导入文件出错！", info: error.localizedDescription)
            return
        }
        switch imported {
        case .english(let words):
            guard let settings else { return }
            // Quiz distractors come from a built-in book: the selected English one, or GRE like the original.
            let isEnglish = Books.catalog.first { $0.tableName == settings.book }?.language == .english
            let options = englishOptions(book: isEnglish ? settings.book : "GRE_2")
            runSession { database, presenter in
                try await EnglishStudySession(database: database, presenter: presenter, options: options)
                    .runRecitation(words, allWords: database.allWords(book: options.book), persist: false)
            }
        case .japanese(let words):
            runSession { database, presenter in
                try await JapaneseStudySession(database: database, presenter: presenter).runRecitation(words: words, number: 0)
            }
        case .custom(let words):
            runSession { _, presenter in await runCustomRecitation(words, presenter: presenter) }
        }
    }

    private var logDirectory: URL? {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first?
            .appendingPathComponent("ToastFish/Log", isDirectory: true)
    }

    /// Saves what a built-in session studied when 自动日志 is on; the files can be re-imported later.
    private func writeLog(_ data: @autoclosure () -> Data, kind: String, empty: Bool) {
        guard settings?.autoLog == true, !empty, let logDirectory else { return }
        do {
            try SessionLog.write(data(), kind: kind, to: logDirectory)
        } catch {
            notifications?.post(title: "ToastFish", body: "保存背诵记录失败：\(error.localizedDescription)")
        }
    }

    @objc private func openLogFolder(_ sender: NSMenuItem) {
        guard let logDirectory else { return }
        try? FileManager.default.createDirectory(at: logDirectory, withIntermediateDirectories: true)
        NSWorkspace.shared.open(logDirectory)
    }

    @objc private func openTemplate(_ sender: NSMenuItem) {
        guard let template = Bundle.main.url(forResource: "自定义模板", withExtension: "xlsx") else {
            notifications?.post(title: "ToastFish", body: "找不到资源：自定义模板.xlsx，请使用打包后的应用。")
            return
        }
        NSWorkspace.shared.activateFileViewerSelecting([template])
    }

    // MARK: Launch at login

    /// Login Items can change in System Settings, so read the state each time the menu opens.
    func menuNeedsUpdate(_ menu: NSMenu) {
        launchItem?.state = SMAppService.mainApp.status == .enabled ? .on : .off
    }

    @objc private func toggleLaunchAtLogin(_ sender: NSMenuItem) {
        let service = SMAppService.mainApp
        do {
            if service.status == .enabled {
                try service.unregister()
            } else {
                try service.register()
            }
        } catch {
            showAlert("设置开机启动失败", info: error.localizedDescription)
        }
        sender.state = service.status == .enabled ? .on : .off
        if service.status == .requiresApproval {
            showAlert("需要在系统设置中允许", info: "请在 系统设置 → 通用 → 登录项 中允许 ToastFish。")
            SMAppService.openSystemSettingsLoginItems()
        }
    }

    private func englishOptions(book: String) -> EnglishStudySession.Options {
        EnglishStudySession.Options(book: book, number: settings?.wordNumber ?? 10,
                                    engType: settings?.engType ?? 2, autoPlay: settings?.autoPlay ?? true)
    }

    /// Replaces any running session with a new one. Only one study flow drives notifications at a time.
    private func runSession(
        _ body: @escaping @MainActor (Database, NotificationPresenter) async throws -> StudyOutcome
    ) {
        guard let database, let presenter, let notifications else { return }
        guard notifications.isAvailable else {
            showAlert("请使用打包后的 ToastFish.app 运行", info: "通知只能在应用包中使用，请先运行 scripts/build_app.sh。")
            return
        }
        let previous = studyTask
        previous?.cancel()
        sessionToken += 1
        let token = sessionToken
        studyTask = Task { @MainActor [weak self] in
            // Let the old session save its finished cards before the new one picks words.
            await previous?.value
            guard !Task.isCancelled, let self else { return }
            self.setSessionHotKeys(enabled: true)
            defer { if self.sessionToken == token { self.setSessionHotKeys(enabled: false) } }
            guard await self.checkNotificationPermission(), !Task.isCancelled else { return }
            do {
                let outcome = try await body(database, presenter)
                if outcome == .cancelled, let error = notifications.lastPostError {
                    self.showAlert("通知发送失败，背诵已停止", info: error)
                }
            } catch {
                self.showDatabaseError(error)
            }
        }
    }

    /// ⌥1–⌥4 and ⌥` are only taken while a session runs, so they type their usual characters otherwise.
    private func setSessionHotKeys(enabled: Bool) {
        guard let hotKeys else { return }
        let buttons: [HotKeys.Key] = [.one, .two, .three, .four]
        if enabled {
            for (index, key) in buttons.enumerated() {
                hotKeys.register(key) { [weak self] in self?.notifications?.answerPending(buttonIndex: index) }
            }
            hotKeys.register(.grave) { [weak self] in self?.notifications?.answerPendingVoice() }
        } else {
            (buttons + [.grave]).forEach(hotKeys.unregister)
        }
    }

    /// Requests permission on first use; refuses to start when notifications are off, and
    /// once per launch points out that action buttons vanish with the banner style.
    private func checkNotificationPermission() async -> Bool {
        guard let notifications else { return false }
        switch await notifications.ensurePermission() {
        case .denied:
            askToOpenNotificationSettings("通知权限未开启",
                info: "ToastFish 通过通知推送单词。请在 系统设置 → 通知 → ToastFish 中允许通知，并把样式设为“提醒”。")
            return false
        case .allowed(let alertStyle):
            if !alertStyle && !checkedAlertStyle {
                checkedAlertStyle = true
                askToOpenNotificationSettings("建议把 ToastFish 的通知样式设为“提醒”",
                    info: "横幅样式的通知几秒后就会消失，按钮也随之隐藏。请在 系统设置 → 通知 → ToastFish 中选择“提醒”。")
            }
            return true
        }
    }

    private func askToOpenNotificationSettings(_ message: String, info: String) {
        let alert = NSAlert()
        alert.messageText = message
        alert.informativeText = info
        alert.addButton(withTitle: "打开通知设置")
        alert.addButton(withTitle: "稍后")
        NSApp.activate(ignoringOtherApps: true)
        if alert.runModal() == .alertFirstButtonReturn,
           let url = URL(string: "x-apple.systempreferences:com.apple.Notifications-Settings.extension?id=\(Bundle.main.bundleIdentifier ?? "")") {
            NSWorkspace.shared.open(url)
        }
    }

    // MARK: Settings

    @objc private func chooseWordNumber(_ sender: NSMenuItem) {
        guard let database, let settings else { return }
        let options = [5, 10, 15, 20]
        guard let index = choose("这次要背多少个？", options: options.map(String.init),
                                 selected: options.firstIndex(of: settings.wordNumber)) else { return }
        let old = settings.wordNumber
        settings.wordNumber = options[index]
        do {
            try database.updateNumber(settings.wordNumber)
            notifications?.post(title: "ToastFish", body: "已设置单词数量为：\(settings.wordNumber)")
        } catch {
            settings.wordNumber = old
            showDatabaseError(error)
        }
    }

    @objc private func chooseEngType(_ sender: NSMenuItem) {
        guard let database, let settings else { return }
        let names = ["美国", "英国"]
        guard let index = choose("请选择发音类型？", options: names, selected: settings.engType == 1 ? 0 : 1) else { return }
        let old = settings.engType
        settings.engType = index == 0 ? 1 : 2
        do {
            try settings.save(to: database)
            notifications?.post(title: "ToastFish", body: "已设置英语类型为：\(names[index])")
        } catch {
            settings.engType = old
            showDatabaseError(error)
        }
    }

    /// A small modal with a pop-up list, replacing the Windows toast selection box.
    private func choose(_ message: String, options: [String], selected: Int?) -> Int? {
        let alert = NSAlert()
        alert.messageText = message
        let popup = NSPopUpButton(frame: NSRect(x: 0, y: 0, width: 160, height: 26), pullsDown: false)
        popup.addItems(withTitles: options)
        popup.selectItem(at: selected ?? 0)
        alert.accessoryView = popup
        alert.addButton(withTitle: "确定")
        alert.addButton(withTitle: "取消")
        NSApp.activate(ignoringOtherApps: true)
        return alert.runModal() == .alertFirstButtonReturn ? popup.indexOfSelectedItem : nil
    }

    private func showAlert(_ message: String, info: String) {
        let alert = NSAlert()
        alert.messageText = message
        alert.informativeText = info
        alert.addButton(withTitle: "好")
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }

    @objc private func toggle(_ sender: NSMenuItem) {
        guard let database, let settings else { return }
        let oldPlay = settings.autoPlay
        let oldLog = settings.autoLog
        if sender.title == "自动播放" { settings.autoPlay.toggle() }
        if sender.title == "自动日志" { settings.autoLog.toggle() }
        do {
            try settings.save(to: database)
        } catch {
            settings.autoPlay = oldPlay
            settings.autoLog = oldLog
            showDatabaseError(error)
            return
        }
        let enabled = sender.title == "自动播放" ? settings.autoPlay : settings.autoLog
        sender.state = enabled ? .on : .off
    }

    @objc private func resetProgress(_ sender: NSMenuItem) {
        guard let database, let settings,
              let book = Books.catalog.first(where: { $0.tableName == settings.book }) else { return }
        let alert = NSAlert()
        alert.messageText = "是否要重置“\(book.displayName)”的学习进度?"
        alert.alertStyle = .warning
        alert.addButton(withTitle: "重置")
        alert.addButton(withTitle: "取消")
        NSApp.activate(ignoringOtherApps: true)
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        do {
            try database.resetTableCount(book: book.tableName)
            notifications?.post(title: "ToastFish", body: "重置\(book.displayName)完成！")
        } catch {
            showDatabaseError(error)
        }
    }

    private func showDatabaseError(_ error: Error) {
        let alert = NSAlert()
        alert.messageText = "数据库操作失败"
        alert.informativeText = error.localizedDescription
        alert.addButton(withTitle: "好")
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }

    @objc private func showShortcuts(_ sender: NSMenuItem) {
        let alert = NSAlert()
        alert.messageText = "快捷方式"
        alert.informativeText = "⌥Q 开始内置单词学习\n⌥` 英语单词发音\n⌥1–⌥4 对应按钮1到4\n\n⌥` 和 ⌥1–⌥4 只在背单词时生效，其余时间不占用这些按键。"
        alert.addButton(withTitle: "好")
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }

    @objc private func openInstructions(_ sender: NSMenuItem) {
        openResource("使用说明", extension: "html")
    }

    @objc private func openStar(_ sender: NSMenuItem) {
        openResource("Star", extension: "pdf")
    }

    private func openResource(_ name: String, extension fileExtension: String) {
        guard let url = Bundle.main.url(forResource: name, withExtension: fileExtension) else {
            notifications?.post(title: "ToastFish", body: "找不到资源：\(name).\(fileExtension)，请使用打包后的应用。")
            return
        }
        if !NSWorkspace.shared.open(url) {
            notifications?.post(title: "ToastFish", body: "无法打开：\(name).\(fileExtension)")
        }
    }

    @objc private func openWebsite(_ sender: NSMenuItem) {
        if let url = URL(string: "https://lab.magiconch.com/toast-fish/") {
            NSWorkspace.shared.open(url)
        }
    }

    @objc private func quit(_ sender: NSMenuItem) {
        NSApp.terminate(nil)
    }

    /// Covers 退出, logout and shutdown: cancelling makes the session save the cards it already finished.
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        notifications?.clearDelivered()
        guard let task = studyTask else { return .terminateNow }
        studyTask = nil
        task.cancel()
        Task { @MainActor in
            await task.value
            NSApp.reply(toApplicationShouldTerminate: true)
        }
        return .terminateLater
    }
}
