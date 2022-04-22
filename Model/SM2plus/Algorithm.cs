using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToastFish.Model.SM2plus
{




    public static class Algorithm
    {
        private const int FirstInterval = 1;
        private const int SecondInterval = 3;
        private const double MinEF = 1.3;
        /*

        public static Card GetUpdatedCard(Card card, Score score)
        {
            return UpdateCard(card, score);
        }

        public static DateTime GetNextDate(Card card, Score score)
        {
            var newCard = UpdateCard(card, score);
            return newCard.NextDate;
        }

        public static int GetInterval(Card card, Score score)
        {
            var newCard = UpdateCard(card, score);
            return newCard.LastInterval;
        }

        public static Card InitCard(Card card)
        {
            card.LastTrainingDate = null;
            card.Score = Score.None;
            card.LastInterval = 0;
            card.NumberOfRepetitions = 0;
            card.EFactor = 2.5;
            card.NextDate = DateTime.UtcNow.Date;
            return card;
        }

        private static Card UpdateCard(Card card, double score)
        {
            card.dateLastReviewed = DateTime.UtcNow;
            card.lastScore = score;
            card.NumberOfRepetitions++;
            //If None - return interval
            if (score == Score.None)
            {
                return ResetCard(card);
            }
            //If first or second time - return interval
            if (card.NumberOfRepetitions == 1)
            {
                card.LastInterval = FirstInterval;
            }
            else if (card.NumberOfRepetitions == 2)
            {
                card.LastInterval = SecondInterval;
            }
            else
            {
                //Calculate new EF
                card.EFactor = CalculateNextEFactor(card.EFactor, score);
                //Calculate and return new interval
                card.LastInterval = CalculateNextInterval(card.LastInterval, card.EFactor);
            }
            card.NextDate = DateTime.UtcNow.Date.AddDays(card.LastInterval);
            return card;
        }

       

        private static double CalculateNextEFactor(double eFactor, Score score)
        {
            //EF':=EF+(0.1-(5-q)*(0.08+(5-q)*0.02))
            var q = (int)score;
            var newEF = eFactor + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02));
            return newEF < MinEF ? MinEF : newEF;
        }

        private static int CalculateNextInterval(int lastInterval, double eFactor)
        {
            return (int)Math.Ceiling(lastInterval * eFactor);
        }*/
    }
}
