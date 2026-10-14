import Foundation

public enum Parameters {
    public static let none = 0.0
    public static let again = 0.4
    public static let hard = 0.6
    public static let good = 0.8
    public static let easy = 1.0
    public static let correct = 0.7
    // Minutes, matching Parameters.cs (including its 5.5-minute hard delay).
    public static let delayAgain = 1.0
    public static let delayHard = 5.5
    public static let delayGood = 10.0
    public static let delayReviewAgain = 10.0
    public static let delayReviewHard = 15.0
    public static let delayReviewGood = 15.0
    public static let difficultyDefaultValue = 0.3
    public static let daysBetweenReviewsDefaultValue = 3.0
}

public enum CardStatus: Int, Sendable, CaseIterable {
    case new = 0, step1, step2, relearnStep1, relearnStep2, reviewed
}

public struct Card: Sendable {
    // DateTime.MinValue in the proleptic Gregorian calendar used by .NET.
    private static let minimumDate = Date(timeIntervalSince1970: -62_135_596_800)
    public var word: Word
    public var difficulty: Double
    public var daysBetweenReviews: Double
    public var dateLastReviewed: Date
    public var dateLearningDue: Date = Card.minimumDate
    public var lastScore: Double
    public var status: CardStatus

    public init(word: Word = Word(), now: Date = Date()) {
        self.word = word
        difficulty = word.difficulty
        daysBetweenReviews = word.daysBetweenReviews
        lastScore = word.lastScore
        status = CardStatus(rawValue: word.status) ?? .new
        // Empty dates retain C#'s minimum date; malformed nonempty dates fall back to now.
        dateLastReviewed = status == .reviewed && !word.dateLastReviewed.isEmpty
            ? ReviewDate.parse(word.dateLastReviewed, now: now) : Self.minimumDate
    }

    public mutating func reset() {
        difficulty = Parameters.difficultyDefaultValue
        daysBetweenReviews = Parameters.daysBetweenReviewsDefaultValue
        lastScore = Parameters.none
    }

    public func isDue(now: Date = Date()) -> Bool { now >= dateLearningDue }

    public var percentOverdue: Double { percentOverdue(now: Date()) }

    public func percentOverdue(now: Date) -> Double {
        guard status == .reviewed else { return 0 }
        return lastScore >= Parameters.correct
            ? min(2, now.timeIntervalSince(dateLastReviewed) / 86_400 / daysBetweenReviews) : 1
    }

    /// `random` returns a value in 0..<1, just like C# Random.NextDouble().
    public mutating func updateCard(
        score: Double, now: () -> Date = { Date() },
        random: () -> Double = {
            var generator = SystemRandomNumberGenerator()
            return Double.random(in: 0..<1, using: &generator)
        }
    ) {
        var isLapsed = false
        switch status {
        case .new, .step1, .step2:
            if score == Parameters.easy || (status == .step2 && score == Parameters.good) {
                status = .reviewed
                dateLastReviewed = now()
                lastScore = score
            } else if score == Parameters.good {
                status = .step2
                dateLearningDue = now().addingTimeInterval(Parameters.delayGood * 60)
            } else if score == Parameters.hard {
                dateLearningDue = now().addingTimeInterval(Parameters.delayHard * 60)
            } else if score == Parameters.again {
                status = .step1
                dateLearningDue = now().addingTimeInterval(Parameters.delayAgain * 60)
            }
        case .reviewed:
            // C# preserves lastScore for normal reviewed answers; only lapses replace it.
            if score == Parameters.again {
                status = .relearnStep1
                isLapsed = true
                dateLearningDue = now().addingTimeInterval(Parameters.delayReviewAgain * 60)
                lastScore = score
            }
        case .relearnStep1, .relearnStep2:
            if score == Parameters.easy || (status == .relearnStep2 && score == Parameters.good) {
                status = .reviewed
                dateLastReviewed = now()
            } else if score == Parameters.good {
                status = .relearnStep2
                dateLearningDue = now().addingTimeInterval(Parameters.delayReviewGood * 60)
            } else if score == Parameters.hard {
                dateLearningDue = now().addingTimeInterval(Parameters.delayReviewHard * 60)
            } else if score == Parameters.again {
                status = .relearnStep1
                dateLearningDue = now().addingTimeInterval(Parameters.delayReviewAgain * 60)
            }
        }
        guard status == .reviewed || isLapsed else { return }

        // Intentional compatibility with Card.cs: overwrite BEFORE measuring the span.
        // A fixed clock therefore gives zero overdue for a correct answer.
        dateLastReviewed = now()
        let correct = score >= Parameters.correct
        let daysSpan = now().timeIntervalSince(dateLastReviewed) / 86_400
        let overdue = correct ? min(2, daysSpan / daysBetweenReviews) : 1
        difficulty = min(1, max(0, difficulty + overdue * (8 - 10 * score) / 17))
        let weight = 3 - 1.7 * difficulty
        if correct {
            daysBetweenReviews *= 1 + (weight - 1) * overdue * (0.95 + 0.1 * random())
        } else {
            daysBetweenReviews *= 1 / (1 + 3 * difficulty)
        }
    }
}

public enum ReviewDate {
    // All cached formatter access is serialized, including legacy time-zone changes.
    // ISO8601DateFormatter is not Sendable; the lock protects these shared instances.
    private static let formatterLock = NSLock()
    nonisolated(unsafe) private static let fractionalISO: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter
    }()
    nonisolated(unsafe) private static let iso: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime]
        return formatter
    }()
    private static let calendarDate = legacyFormatter(format: "yyyy-MM-dd")
    private static let legacyFormatters = [
        "yyyy/M/d H:mm:ss", "M/d/yyyy h:mm:ss a", "yyyy-MM-dd HH:mm:ss",
        "M/d/yyyy H:mm:ss", "dd.MM.yyyy HH:mm:ss",
    ].map { legacyFormatter(format: $0) }

    private static func legacyFormatter(format: String) -> DateFormatter {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.isLenient = false
        formatter.dateFormat = format
        return formatter
    }

    public static func string(from date: Date) -> String {
        formatterLock.withLock { fractionalISO.string(from: date) }
    }

    /// Legacy DateTime.ToString() has no zone, so interpret it in the user's local zone.
    public static func parse(_ text: String, now: Date = Date(), timeZone: TimeZone = .current) -> Date {
        guard !text.isEmpty else { return now }
        return formatterLock.withLock {
            calendarDate.timeZone = timeZone
            for formatter in [fractionalISO, iso] {
                if let date = formatter.date(from: text) {
                    // ISO8601DateFormatter normalizes invalid days (e.g. February 30).
                    // DateTime.TryParse rejects them, so validate the calendar date too.
                    return calendarDate.date(from: String(text.prefix(10))) == nil ? now : date
                }
            }
            for formatter in legacyFormatters {
                formatter.timeZone = timeZone
                if let date = formatter.date(from: text) { return date }
            }
            return now
        }
    }
}
