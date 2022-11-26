using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToastFish.Model.SqliteControl;

namespace ToastFish.Model.SM2plus
{
    public enum Cardstatus
    {
        New = 0,
        Step1 = 1,
        Step2 = 2,
        RelearnStep1 = 3,
        RelearnStep2 = 4,
        Reviewed = 5
    }
    public class Card
    {
        public double difficulty { get; set; }
        public double daysBetweenReviews { get; set; }
        public DateTime dateLastReviewed { get; set; }
        public DateTime dateLearingDue { get; set; }
        public double lastScore { get; set; }
        public Cardstatus status;
        public Word word;

        public double percentOverdue
        {
            get
            {
                double podue = 0;
                if (status == Cardstatus.Reviewed)
                {
                    bool correct = lastScore >= Parameters.Correct;
                    TimeSpan daysSpan = new TimeSpan(DateTime.Now.Ticks - dateLastReviewed.Ticks);

                    if (correct)
                        podue = Math.Min(2, daysSpan.TotalDays / daysBetweenReviews);
                    else
                        podue = 1;
                }
                else if (status == Cardstatus.New || status == Cardstatus.Step1)
                {
                }

                return podue;
            }
        }
        public Card()
        {
            difficulty = Parameters.diffcultyDefaultValue;
            daysBetweenReviews = Parameters.daysBetweenReviewsDefaultValue;
            lastScore = 0;
            status = Cardstatus.New;
        }
        public Card(Word wd)
        {
            word = wd;
            difficulty = wd.difficulty;
            daysBetweenReviews = wd.daysBetweenReviews;
            lastScore = wd.lastScore;
            status = (Cardstatus)wd.status;

            bool isSuccess1;
            if (status == Cardstatus.Reviewed)
            {
                if ((wd.dateLastReviewed != null) && (wd.dateLastReviewed != ""))
                {
                    isSuccess1 = DateTime.TryParse(wd.dateLastReviewed, out DateTime tempDT);
                    if (isSuccess1)
                        dateLastReviewed = tempDT;//update                    
                    else
                        dateLastReviewed = DateTime.Now;
                }
            }

        }

        public void reset()
        {
            difficulty = Parameters.diffcultyDefaultValue;
            daysBetweenReviews = Parameters.daysBetweenReviewsDefaultValue;
            lastScore = Parameters.None;
        }

        public bool isDue()
        {
            bool rst = false;
            DateTime curTime = DateTime.Now;
            if (curTime >= dateLearingDue)
                rst = true;
            return rst;
        }


        public void updateCard(double curScore)
        {
            bool isLapsed = false;
            switch (status)
            {
                case Cardstatus.New:
                case Cardstatus.Step1:
                    if (curScore == Parameters.Easy)
                    {
                        status = Cardstatus.Reviewed;
                        dateLastReviewed = DateTime.Now;
                        lastScore = curScore;
                    }
                    else if (curScore == Parameters.Good)
                    {
                        status = Cardstatus.Step2;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayGood);
                    }
                    else if (curScore == Parameters.Hard)
                    {
                        //status = Cardstatus.Step1;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayHard);
                    }
                    else if (curScore == Parameters.Again)
                    {
                        status = Cardstatus.Step1;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayAgain);
                    }
                    break;
                case Cardstatus.Step2:
                    if (curScore == Parameters.Easy)
                    {
                        status = Cardstatus.Reviewed;
                        dateLastReviewed = DateTime.Now;
                        lastScore = curScore;
                    }
                    else if (curScore == Parameters.Good)
                    {
                        status = Cardstatus.Reviewed;
                        dateLastReviewed = DateTime.Now;
                        lastScore = curScore;
                    }
                    else if (curScore == Parameters.Hard)
                    {
                        //status = Cardstatus.Step2;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayHard);
                    }
                    else if (curScore == Parameters.Again)
                    {
                        status = Cardstatus.Step1;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayAgain);
                    }
                    break;
                case Cardstatus.Reviewed:
                    if (curScore == Parameters.Again)
                    {
                        status = Cardstatus.RelearnStep1;
                        isLapsed = true;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayReviewAgain);
                        lastScore = curScore;
                    }
                    break;
                case Cardstatus.RelearnStep1:
                    //isLapsed = true; do not update last score for relearn steps
                    if (curScore == Parameters.Easy)
                    {
                        status = Cardstatus.Reviewed;
                        dateLastReviewed = DateTime.Now;
                    }
                    else if (curScore == Parameters.Good)
                    {
                        status = Cardstatus.RelearnStep2;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayReviewGood);
                    }
                    else if (curScore == Parameters.Hard)
                    {
                        //status = Cardstatus.RelearnStep1;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayReviewHard);
                    }
                    else if (curScore == Parameters.Again)
                    {
                        status = Cardstatus.RelearnStep1;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayReviewAgain);
                    }
                    break;
                case Cardstatus.RelearnStep2:
                    //isLapsed = true;
                    if (curScore == Parameters.Easy)
                    {
                        status = Cardstatus.Reviewed;
                        dateLastReviewed = DateTime.Now;
                    }
                    else if (curScore == Parameters.Good)
                    {
                        status = Cardstatus.Reviewed;
                        dateLastReviewed = DateTime.Now;
                    }
                    else if (curScore == Parameters.Hard)
                    {
                        //status = Cardstatus.RelearnStep2;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayReviewHard);
                    }
                    else if (curScore == Parameters.Again)
                    {
                        status = Cardstatus.RelearnStep1;
                        dateLearingDue = DateTime.Now.AddMinutes(Parameters.delayReviewAgain);
                    }
                    break;
                default:
                    break;
            }
            if ((status != Cardstatus.Reviewed) && (isLapsed != true)) return;

            dateLastReviewed = DateTime.Now;
            bool correct = curScore >= Parameters.Correct;
            TimeSpan daysSpan = new TimeSpan(DateTime.Now.Ticks - dateLastReviewed.Ticks);
            double podue;
            if (correct)
                podue = Math.Min(2, daysSpan.TotalDays / daysBetweenReviews);
            else
                podue = 1;
            difficulty += podue * (8 - 10 * curScore) / 17;
            if (difficulty < 0)
                difficulty = 0;
            if (difficulty > 1)
                difficulty = 1;
            double dfweight = 3 - 1.7 * difficulty;
            Random rnd = new Random();
            if (correct)
                daysBetweenReviews *= (1 + (dfweight - 1) * podue * (0.95 + 0.1 * rnd.NextDouble()));
            else
                daysBetweenReviews *= 1 / (1 + 3 * difficulty);
        }
    }
}
