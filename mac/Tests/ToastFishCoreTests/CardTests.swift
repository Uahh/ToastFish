import Foundation
import Testing
@testable import ToastFishCore

private let instant = Date(timeIntervalSince1970: 1_700_000_000)

@Test(arguments: CardStatus.allCases, [Parameters.again, Parameters.hard, Parameters.good, Parameters.easy])
func everyLearningTransition(status: CardStatus, score: Double) {
    // Rows are New, Step1, Step2, RelearnStep1, RelearnStep2, Reviewed;
    // columns are Again, Hard, Good, Easy. Expected values come from Card.cs.
    let states: [[CardStatus]] = [
        [.step1, .new, .step2, .reviewed],
        [.step1, .step1, .step2, .reviewed],
        [.step1, .step2, .reviewed, .reviewed],
        [.relearnStep1, .relearnStep1, .relearnStep2, .reviewed],
        [.relearnStep1, .relearnStep2, .reviewed, .reviewed],
        [.relearnStep1, .reviewed, .reviewed, .reviewed],
    ]
    let delays: [[Double?]] = [
        [1, 5.5, 10, nil], [1, 5.5, 10, nil], [1, 5.5, nil, nil],
        [10, 15, 15, nil], [10, 15, nil, nil], [10, nil, nil, nil],
    ]
    let column = [0.4, 0.6, 0.8, 1].firstIndex(of: score)!
    var card = Card(word: Word(status: status.rawValue), now: instant)
    card.lastScore = 0.6
    let oldReview = card.dateLastReviewed
    let oldDue = card.dateLearningDue
    card.updateCard(score: score, now: { instant }, random: { 0.5 })
    let expected = states[status.rawValue][column]
    #expect(card.status == expected)
    if let delay = delays[status.rawValue][column] {
        #expect(card.dateLearningDue == instant.addingTimeInterval(delay * 60))
        #expect(!card.isDue(now: instant.addingTimeInterval(delay * 60 - 1)))
        #expect(card.isDue(now: instant.addingTimeInterval(delay * 60)))
    } else {
        #expect(card.dateLearningDue == oldDue)
    }
    let didReview = expected == .reviewed || status == .reviewed
    #expect(card.dateLastReviewed == (didReview ? instant : oldReview))
    let recordsScore = (status.rawValue <= 2 && expected == .reviewed)
        || (status == .reviewed && score == 0.4)
    #expect(card.lastScore == (recordsScore ? score : 0.6))
    if status == .reviewed && score == 0.4 {
        #expect(abs(card.difficulty - (0.3 + 4.0 / 17)) < 1e-12)
        #expect(abs(card.daysBetweenReviews - 3 / (1 + 3 * (0.3 + 4.0 / 17))) < 1e-12)
    } else if status == .reviewed && score == 0.6 {
        #expect(abs(card.difficulty - (0.3 + 2.0 / 17)) < 1e-12)
        #expect(abs(card.daysBetweenReviews - 3 / (1 + 3 * (0.3 + 2.0 / 17))) < 1e-12)
    } else {
        #expect(card.difficulty == 0.3)
        #expect(card.daysBetweenReviews == 3)
    }
}

@Test func intervalUsesClockAfterOverwritingReviewDate() {
    var card = Card(word: Word(status: 5), now: instant)
    card.dateLastReviewed = instant.addingTimeInterval(-100 * 86_400)
    // Deliberately move the clock between the two reads: overdue = 1 / 3.
    var reads = 0
    card.updateCard(score: 1, now: {
        reads += 1
        return reads == 1 ? instant : instant.addingTimeInterval(86_400)
    }, random: { 0.5 })
    let expectedDifficulty = 0.3 - 2.0 / 51
    let expectedInterval = 3 * (1 + (2 - 1.7 * expectedDifficulty) / 3)
    #expect(abs(card.difficulty - expectedDifficulty) < 1e-12)
    #expect(abs(card.daysBetweenReviews - expectedInterval) < 1e-12)
    #expect(card.dateLastReviewed == instant)
}

@Test func difficultyClampsAndOverdueCaps() {
    var card = Card(word: Word(status: 5), now: instant)
    card.difficulty = 0.95
    card.updateCard(score: 0.4, now: { instant }, random: { 0 })
    #expect(card.difficulty == 1)
    #expect(card.daysBetweenReviews == 0.75)
    card.status = .reviewed
    card.difficulty = 0.01
    var reads = 0
    card.updateCard(score: 1, now: {
        reads += 1
        return instant.addingTimeInterval(reads == 1 ? 0 : 10 * 86_400)
    }, random: { 0 })
    #expect(card.difficulty == 0)
    #expect(abs(card.daysBetweenReviews - 3.6) < 1e-12) // .75 * (1 + 2 * 2 * .95)
    card.lastScore = 0.8
    card.dateLastReviewed = instant
    #expect(card.percentOverdue(now: instant.addingTimeInterval(100 * 86_400)) == 2)
    #expect(abs(card.percentOverdue(now: instant.addingTimeInterval(1.8 * 86_400)) - 0.5) < 1e-12)
    card.lastScore = 0.4
    #expect(card.percentOverdue(now: instant) == 1)
    card.status = .step2
    #expect(card.percentOverdue(now: instant) == 0)
    card.reset()
    #expect(card.difficulty == 0.3 && card.daysBetweenReviews == 3 && card.lastScore == 0)
    #expect(card.status == .step2) // reset() does not reset the status in C#.
}

@Test func legacyAndISOReviewDates() {
    let zone = TimeZone(secondsFromGMT: 0)!
    let expected = Date(timeIntervalSince1970: 1_633_269_906)
    for text in ["2021-10-03T14:05:06Z", "2021/10/3 14:05:06", "10/3/2021 2:05:06 PM"] {
        #expect(ReviewDate.parse(text, now: instant, timeZone: zone) == expected)
    }
    #expect(ReviewDate.parse("invalid", now: instant) == instant)
    #expect(ReviewDate.parse("", now: instant) == instant)
    #expect(ReviewDate.parse("2021-02-30T14:05:06Z", now: instant) == instant)
    #expect(ReviewDate.parse(ReviewDate.string(from: instant), now: .distantPast) == instant)
}

@Test func cardInitializationPreservesMissingDateAndFallsBackForInvalidDate() {
    // C# leaves DateTime at its minimum when the stored date is null/empty.
    let minimum = Date(timeIntervalSince1970: -62_135_596_800)
    var word = Word(status: 5)
    word.lastScore = 0.8
    let missing = Card(word: word, now: instant)
    #expect(missing.dateLastReviewed == minimum)
    #expect(missing.dateLearningDue == minimum)
    #expect(missing.isDue(now: instant))
    #expect(missing.percentOverdue(now: instant) == 2)
    word.dateLastReviewed = "not a date"
    #expect(Card(word: word, now: instant).dateLastReviewed == instant)
    word.dateLastReviewed = "2021-10-03T14:05:06Z"
    word.status = 1
    #expect(Card(word: word, now: instant).dateLastReviewed == minimum)
}
