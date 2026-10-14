import Compression
import Foundation

public enum ZipError: Error, LocalizedError {
    case notAZip
    case unsupported(String)
    case corrupt(String)

    public var errorDescription: String? {
        switch self {
        case .notAZip: return "不是有效的 xlsx 文件"
        case .unsupported(let detail): return "不支持的文件格式：\(detail)"
        case .corrupt(let detail): return "文件已损坏：\(detail)"
        }
    }
}

/// Just enough ZIP for .xlsx files: read stored/deflated entries, write stored ones.
public enum ZipArchive {
    /// Refuse entries that would inflate past these limits (a word list is a few MB at most).
    static let maxEntrySize = 100 << 20
    static let maxTotalSize = 200 << 20

    /// Returns the file entries accepted by `include`, keyed by their path inside the archive.
    /// Other entries (images, printer settings…) are never decompressed.
    public static func read(_ data: Data, include: (String) -> Bool = { _ in true }) throws -> [String: Data] {
        let bytes = [UInt8](data)
        guard bytes.count >= 22 else { throw ZipError.notAZip }
        // The end-of-central-directory record sits in the last 22 + 65535 bytes.
        var eocd = -1
        var index = bytes.count - 22
        while index >= max(0, bytes.count - 22 - 65_535) {
            if u32(bytes, index) == 0x0605_4B50 { eocd = index; break }
            index -= 1
        }
        guard eocd >= 0 else { throw ZipError.notAZip }
        let count = Int(u16(bytes, eocd + 10))
        var offset = Int(u32(bytes, eocd + 16))
        if count == 0xFFFF || offset == 0xFFFF_FFFF { throw ZipError.unsupported("ZIP64") }

        var entries: [String: Data] = [:]
        var total = 0
        for _ in 0..<count {
            guard offset + 46 <= bytes.count, u32(bytes, offset) == 0x0201_4B50 else { throw ZipError.corrupt("目录") }
            let flags = u16(bytes, offset + 8)
            let method = u16(bytes, offset + 10)
            let crc = u32(bytes, offset + 16)
            let compressedSize = Int(u32(bytes, offset + 20))
            let size = Int(u32(bytes, offset + 24))
            let nameLength = Int(u16(bytes, offset + 28))
            let extraLength = Int(u16(bytes, offset + 30))
            let commentLength = Int(u16(bytes, offset + 32))
            let localOffset = Int(u32(bytes, offset + 42))
            guard offset + 46 + nameLength <= bytes.count else { throw ZipError.corrupt("文件名") }
            let name = String(decoding: bytes[(offset + 46)..<(offset + 46 + nameLength)], as: UTF8.self)
            offset += 46 + nameLength + extraLength + commentLength

            if name.hasSuffix("/") || !include(name) { continue }
            if flags & 1 != 0 { throw ZipError.unsupported("加密文件") }
            total += size
            guard size <= maxEntrySize, total <= maxTotalSize else { throw ZipError.unsupported("文件过大") }
            guard localOffset + 30 <= bytes.count, u32(bytes, localOffset) == 0x0403_4B50 else { throw ZipError.corrupt(name) }
            let start = localOffset + 30 + Int(u16(bytes, localOffset + 26)) + Int(u16(bytes, localOffset + 28))
            guard start + compressedSize <= bytes.count else { throw ZipError.corrupt(name) }
            let payload = Data(bytes[start..<(start + compressedSize)])
            let content: Data
            switch method {
            case 0: content = payload
            case 8: content = try inflate(payload, size: size, name: name)
            default: throw ZipError.unsupported("压缩方式 \(method)")
            }
            // Catches truncated streams and wrong declared sizes.
            guard content.count == size, crc32(content) == crc else { throw ZipError.corrupt(name) }
            entries[name] = content
        }
        return entries
    }

    /// Writes the entries uncompressed, in the given order.
    public static func write(_ entries: [(name: String, data: Data)]) -> Data {
        var output = Data()
        var directory = Data()
        for entry in entries {
            let name = Data(entry.name.utf8)
            let crc = crc32(entry.data)
            let localOffset = UInt32(output.count)
            // Local file header; general flag bit 11 marks UTF-8 names.
            output.append(le32(0x0403_4B50)); output.append(le16(20)); output.append(le16(0x0800)); output.append(le16(0))
            output.append(le16(0)); output.append(le16(0x21))  // time 00:00, date 1980-01-01
            output.append(le32(crc)); output.append(le32(UInt32(entry.data.count))); output.append(le32(UInt32(entry.data.count)))
            output.append(le16(UInt16(name.count))); output.append(le16(0))
            output.append(name); output.append(entry.data)

            directory.append(le32(0x0201_4B50)); directory.append(le16(20)); directory.append(le16(20)); directory.append(le16(0x0800))
            directory.append(le16(0)); directory.append(le16(0)); directory.append(le16(0x21))
            directory.append(le32(crc)); directory.append(le32(UInt32(entry.data.count))); directory.append(le32(UInt32(entry.data.count)))
            directory.append(le16(UInt16(name.count))); directory.append(le16(0)); directory.append(le16(0))
            directory.append(le16(0)); directory.append(le16(0)); directory.append(le32(0)); directory.append(le32(localOffset))
            directory.append(name)
        }
        let directoryOffset = UInt32(output.count)
        output.append(directory)
        output.append(le32(0x0605_4B50)); output.append(le16(0)); output.append(le16(0))
        output.append(le16(UInt16(entries.count))); output.append(le16(UInt16(entries.count)))
        output.append(le32(UInt32(directory.count))); output.append(le32(directoryOffset)); output.append(le16(0))
        return output
    }

    private static func inflate(_ data: Data, size: Int, name: String) throws -> Data {
        if size == 0 { return Data() }
        var output = Data(count: size)
        let written = output.withUnsafeMutableBytes { out in
            data.withUnsafeBytes { input in
                // COMPRESSION_ZLIB is raw DEFLATE, which is what ZIP method 8 stores.
                compression_decode_buffer(out.bindMemory(to: UInt8.self).baseAddress!, size,
                                          input.bindMemory(to: UInt8.self).baseAddress!, data.count,
                                          nil, COMPRESSION_ZLIB)
            }
        }
        guard written == size else { throw ZipError.corrupt(name) }
        return output
    }

    private static let crcTable: [UInt32] = (0..<256).map { n -> UInt32 in
        var c = UInt32(n)
        for _ in 0..<8 { c = c & 1 != 0 ? 0xEDB8_8320 ^ (c >> 1) : c >> 1 }
        return c
    }

    static func crc32(_ data: Data) -> UInt32 {
        var crc: UInt32 = 0xFFFF_FFFF
        for byte in data { crc = crcTable[Int((crc ^ UInt32(byte)) & 0xFF)] ^ (crc >> 8) }
        return crc ^ 0xFFFF_FFFF
    }

    private static func u16(_ b: [UInt8], _ i: Int) -> UInt16 { UInt16(b[i]) | UInt16(b[i + 1]) << 8 }
    private static func u32(_ b: [UInt8], _ i: Int) -> UInt32 {
        UInt32(b[i]) | UInt32(b[i + 1]) << 8 | UInt32(b[i + 2]) << 16 | UInt32(b[i + 3]) << 24
    }
    private static func le16(_ v: UInt16) -> Data { Data([UInt8(v & 0xFF), UInt8(v >> 8)]) }
    private static func le32(_ v: UInt32) -> Data { Data((0..<4).map { UInt8((v >> (8 * $0)) & 0xFF) }) }
}
