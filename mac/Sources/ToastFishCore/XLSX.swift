import Foundation

/// Minimal .xlsx support: the first worksheet as rows of strings, and a one-sheet writer.
public enum XLSX {
    /// Rows of the first sheet. Row and column gaps are kept as empty strings so indexes match Excel's.
    public static func readFirstSheet(_ data: Data) throws -> [[String]] {
        let files = try ZipArchive.read(data) { $0.hasSuffix(".xml") || $0.hasSuffix(".rels") }
        let shared = try files["xl/sharedStrings.xml"].map(parseSharedStrings) ?? []
        guard let sheetPath = firstSheetPath(files), let sheet = files[sheetPath] else {
            throw ZipError.corrupt("找不到工作表")
        }
        return try parseSheet(sheet, sharedStrings: shared)
    }

    /// A single sheet named "Word" with inline strings; empty strings leave the cell out.
    public static func write(rows: [[String]], columnWidths: [Int: Double] = [:]) -> Data {
        var sheet = #"<?xml version="1.0" encoding="UTF-8" standalone="yes"?>"#
        sheet += #"<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">"#
        if !columnWidths.isEmpty {
            sheet += "<cols>"
            for (column, width) in columnWidths.sorted(by: { $0.key < $1.key }) {
                sheet += #"<col min="\#(column + 1)" max="\#(column + 1)" width="\#(width)" customWidth="1"/>"#
            }
            sheet += "</cols>"
        }
        sheet += "<sheetData>"
        for (r, row) in rows.enumerated() {
            sheet += #"<row r="\#(r + 1)">"#
            for (c, value) in row.enumerated() where !value.isEmpty {
                sheet += #"<c r="\#(columnName(c))\#(r + 1)" t="inlineStr"><is><t xml:space="preserve">\#(escape(value))</t></is></c>"#
            }
            sheet += "</row>"
        }
        sheet += "</sheetData></worksheet>"

        let contentTypes = #"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
            """#
        let rootRels = #"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
            """#
        let workbook = #"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Word" sheetId="1" r:id="rId1"/></sheets></workbook>
            """#
        let workbookRels = #"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
            """#
        // The smallest stylesheet strict readers accept: one font, the two required fills, one border, one format.
        let styles = #"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs></styleSheet>
            """#
        return ZipArchive.write([
            ("[Content_Types].xml", Data(contentTypes.utf8)),
            ("_rels/.rels", Data(rootRels.utf8)),
            ("xl/workbook.xml", Data(workbook.utf8)),
            ("xl/_rels/workbook.xml.rels", Data(workbookRels.utf8)),
            ("xl/worksheets/sheet1.xml", Data(sheet.utf8)),
            ("xl/styles.xml", Data(styles.utf8)),
        ])
    }

    static func columnName(_ index: Int) -> String {
        var name = ""
        var n = index + 1
        while n > 0 {
            let r = (n - 1) % 26
            name = String(UnicodeScalar(UInt8(65 + r))) + name
            n = (n - 1) / 26
        }
        return name
    }

    /// Excel's sheet limits; anything beyond them is a broken or hostile file.
    static let maxColumns = 16_384
    static let maxRows = 1_048_576

    /// "B12" -> 1, nil when the reference has no letters or more than Excel's three ("XFD").
    static func columnIndex(_ reference: String) -> Int? {
        var value = 0
        var letters = 0
        for scalar in reference.unicodeScalars {
            guard (65...90).contains(scalar.value) || (97...122).contains(scalar.value) else { break }
            letters += 1
            guard letters <= 3 else { return nil }
            value = value * 26 + Int((scalar.value & 0xDF) - 64)
        }
        return letters > 0 ? value - 1 : nil
    }

    private static func escape(_ text: String) -> String {
        // Control characters other than tab/newline are invalid in XML 1.0.
        var out = ""
        for scalar in text.unicodeScalars {
            switch scalar {
            case "&": out += "&amp;"
            case "<": out += "&lt;"
            case ">": out += "&gt;"
            case "\"": out += "&quot;"
            case "\t", "\n", "\r": out.unicodeScalars.append(scalar)
            case _ where scalar.value < 0x20: continue
            default: out.unicodeScalars.append(scalar)
            }
        }
        return out
    }

    private static func firstSheetPath(_ files: [String: Data]) -> String? {
        // workbook.xml names the first sheet's relationship id; the rels file maps it to a path.
        if let workbook = files["xl/workbook.xml"], let rels = files["xl/_rels/workbook.xml.rels"] {
            let sheets = ElementCollector(element: "sheet").collect(workbook)
            let relations = ElementCollector(element: "Relationship").collect(rels)
            if let id = sheets.first?["r:id"],
               let target = relations.first(where: { $0["Id"] == id })?["Target"] {
                return target.hasPrefix("/") ? String(target.dropFirst()) : "xl/" + target
            }
        }
        return files.keys.filter { $0.hasPrefix("xl/worksheets/") && $0.hasSuffix(".xml") }.sorted().first
    }

    private static func parseSharedStrings(_ data: Data) throws -> [String] {
        let parser = SharedStringsParser()
        try parser.parse(data)
        return parser.strings
    }

    private static func parseSheet(_ data: Data, sharedStrings: [String]) throws -> [[String]] {
        let parser = SheetParser(sharedStrings: sharedStrings)
        try parser.parse(data)
        return parser.rows
    }
}

/// "x:c" -> "c", for packages written with a namespace prefix.
private func localName(_ name: String) -> Substring {
    name.split(separator: ":").last ?? Substring(name)
}

private class XMLDelegate: NSObject, XMLParserDelegate {
    func parse(_ data: Data) throws {
        let parser = XMLParser(data: data)
        parser.delegate = self
        // XMLParser's own errors read as gibberish to users; report a damaged sheet instead.
        guard parser.parse() else { throw ZipError.corrupt("工作表内容") }
    }
}

/// Collects attributes of every element with a given (qualified) name.
private final class ElementCollector: XMLDelegate {
    let element: String
    var found: [[String: String]] = []

    init(element: String) { self.element = element }

    func collect(_ data: Data) -> [[String: String]] {
        try? parse(data)
        return found
    }

    func parser(_ parser: XMLParser, didStartElement name: String, namespaceURI: String?, qualifiedName: String?,
                attributes: [String: String] = [:]) {
        if name == element || name.hasSuffix(":" + element) { found.append(attributes) }
    }
}

/// <si> entries: plain <t>, or rich-text runs <r><t>; phonetic <rPh> hints are skipped.
private final class SharedStringsParser: XMLDelegate {
    var strings: [String] = []
    private var current = ""
    private var inText = false
    private var phoneticDepth = 0

    func parser(_ parser: XMLParser, didStartElement name: String, namespaceURI: String?, qualifiedName: String?,
                attributes: [String: String] = [:]) {
        switch localName(name) {
        case "si": current = ""
        case "rPh": phoneticDepth += 1
        case "t": inText = phoneticDepth == 0
        default: break
        }
    }

    func parser(_ parser: XMLParser, foundCharacters string: String) {
        if inText { current += string }
    }

    func parser(_ parser: XMLParser, didEndElement name: String, namespaceURI: String?, qualifiedName: String?) {
        switch localName(name) {
        case "si": strings.append(current)
        case "rPh": phoneticDepth -= 1
        case "t": inText = false
        default: break
        }
    }
}

private final class SheetParser: XMLDelegate {
    let sharedStrings: [String]
    var rows: [[String]] = []
    private var rowIndex = -1
    private var column = 0
    private var type = ""
    private var value = ""
    private var collecting = false
    private var phoneticDepth = 0

    init(sharedStrings: [String]) { self.sharedStrings = sharedStrings }

    func parser(_ parser: XMLParser, didStartElement name: String, namespaceURI: String?, qualifiedName: String?,
                attributes: [String: String] = [:]) {
        switch localName(name) {
        case "row":
            rowIndex = attributes["r"].flatMap(Int.init).map { $0 - 1 } ?? rowIndex + 1
            column = 0
            if !(0..<XLSX.maxRows).contains(rowIndex) { parser.abortParsing() }
        case "c":
            if let reference = attributes["r"] {
                guard let index = XLSX.columnIndex(reference) else { parser.abortParsing(); return }
                column = index
            }
            if column >= XLSX.maxColumns { parser.abortParsing() }
            type = attributes["t"] ?? "n"
            value = ""
        case "rPh": phoneticDepth += 1
        case "v", "t": collecting = phoneticDepth == 0
        default: break
        }
    }

    func parser(_ parser: XMLParser, foundCharacters string: String) {
        if collecting { value += string }
    }

    func parser(_ parser: XMLParser, didEndElement name: String, namespaceURI: String?, qualifiedName: String?) {
        switch localName(name) {
        case "v", "t": collecting = false
        case "rPh": phoneticDepth -= 1
        case "c":
            var text = value
            if type == "s" {
                text = Int(value).flatMap { sharedStrings.indices.contains($0) ? sharedStrings[$0] : nil } ?? ""
            } else if type == "b" {
                text = value == "1" ? "TRUE" : "FALSE"
            }
            if !text.isEmpty, (0..<XLSX.maxRows).contains(rowIndex), column < XLSX.maxColumns {
                while rows.count <= rowIndex { rows.append([]) }
                while rows[rowIndex].count <= column { rows[rowIndex].append("") }
                rows[rowIndex][column] = text
            }
            column += 1
        default: break
        }
    }
}
