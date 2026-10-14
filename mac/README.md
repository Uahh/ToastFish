# ToastFish for macOS

ToastFish 的原生 macOS 版本：常驻菜单栏，用系统通知推送单词，在通知按钮上作答。功能与 Windows 版基本一致，词库和学习算法相同。

- 最低系统：macOS 13 Ventura
- 技术栈：Swift 6、SwiftPM、AppKit、UserNotifications，没有第三方依赖
- 构建只需 Command Line Tools，不需要 Xcode

## 构建与运行

在仓库根目录执行：

```sh
mac/scripts/build_app.sh          # 生成 mac/build/ToastFish.app
mac/scripts/build_app.sh --zip    # 另外生成 mac/build/ToastFish-macOS-<版本>.zip，用来发给别人
open mac/build/ToastFish.app
```

脚本编译同时支持 Apple 芯片和 Intel 的通用版本，生成图标，复制词库和资源，并做 ad-hoc 签名。可以把 `ToastFish.app` 拖进“应用程序”文件夹使用。分享给别人时发 zip 文件即可，不需要源码。

应用只有 ad-hoc 签名、没有经过公证。从网上下载或通过聊天工具收到的副本会被 Gatekeeper 拦下：第一次打开被拦后，到 **系统设置 → 隐私与安全性** 底部点“仍要打开”（macOS 13–14 也可以在 Finder 里右键 → 打开），或者执行：

```sh
xattr -dr com.apple.quarantine /Applications/ToastFish.app
```

开发时单独编译和测试：

```sh
cd mac
swift build
swift test
```

测试使用 Swift Testing。`Package.swift` 为只装了 Command Line Tools 的环境补上测试框架路径；这套框架运行测试需要 macOS 14 或更高版本，应用本身仍支持 macOS 13。

## 第一次使用

1. 点菜单栏的小鱼图标 → **开始！**，在弹窗中允许通知。
2. 打开 **系统设置 → 通知 → ToastFish**，把样式设为 **提醒**。横幅样式几秒后就会消失，按钮也一起消失。
3. macOS 把多个按钮收在通知的 **选项** 里，鼠标移到通知上即可看到。

完整说明见菜单 **使用说明 → 使用说明(必读)**。

## 功能对照

| Windows 版 | macOS 版 |
| --- | --- |
| 托盘图标和右键菜单 | 菜单栏图标，菜单结构相同 |
| Windows 通知按钮 | 带操作按钮的系统通知 |
| 下拉框设置单词个数、英标类型 | 小弹窗选择 |
| Alt+Q、Alt+1–4、Alt+~ | ⌥Q、⌥1–⌥4、⌥`（⌥1–4 和 ⌥` 只在背诵或测验进行时占用；⌥Q 一直占用） |
| 有道发音 + Windows 语音 | 有道发音（缓存）+ 系统语音 |
| 安装目录下的 Log 文件夹 | `~/Library/Application Support/ToastFish/Log`（菜单可直接打开） |
| 导入 .xlsx / .xls | 只支持 .xlsx |
| 启动文件夹快捷方式 | 系统登录项 |

另外修了几处原版的问题：中途重新开始或退出时会保存已经背完的词；五十音进度循环到最后一个后不会出错；导入日语和自定义 Excel 时不会漏掉最后一行。

## 数据位置

| 内容 | 位置 |
| --- | --- |
| 学习进度（词库数据库） | `~/Library/Application Support/ToastFish/inami.db` |
| 背诵记录 | `~/Library/Application Support/ToastFish/Log/` |
| 发音缓存 | `~/Library/Caches/ToastFish/Mp3Cache/` |

数据库在第一次启动时从应用包复制出来，以后更新应用不会覆盖。**重置进度**把当前词库恢复为未学习、进度归零，保留词库内容和复习参数，不影响其他词库。

## 代码结构

- `Sources/ToastFishCore`：与界面无关的部分，包括词库数据库、SM2 记忆算法、各种背诵和测验流程、Excel 读写。全部有单元测试。
- `Sources/ToastFish`：菜单栏、通知、发音、快捷键、登录项。
- `Tests/ToastFishCoreTests`：单元测试，用仓库里 `Resources/inami.db` 的临时副本运行。
- `scripts/build_app.sh`：打包脚本。
