using TopSpeed.Localization;

namespace TopSpeed.Game
{
    internal sealed class ResultCatalog
    {
        public static readonly string[] WinnerTitles =
        {
            LocalizationService.Mark("Congratulations! You have made it to the first position."),
            LocalizationService.Mark("Outstanding drive. You secured first position."),
            LocalizationService.Mark("First position achieved. Great race.")
        };

        public static readonly string[] NonWinnerTitles =
        {
            LocalizationService.Mark("Race complete."),
            LocalizationService.Mark("The race has finished."),
            LocalizationService.Mark("Final results are ready.")
        };

        public static readonly string[] WinnerCaptions =
        {
            LocalizationService.Mark("The following are the details of all players."),
            LocalizationService.Mark("Here are the final standings for everyone.")
        };

        public static readonly string[] NonWinnerCaptions =
        {
            LocalizationService.Mark("Here are the final standings."),
            LocalizationService.Mark("The following are the race details for all players.")
        };

        public static readonly string[] TimeTrialRecordTitles =
        {
            LocalizationService.Mark("Outstanding run! New personal record."),
            LocalizationService.Mark("Excellent driving. You beat your previous best time."),
            LocalizationService.Mark("Brilliant result! You set a new best time.")
        };

        public static readonly string[] TimeTrialNoRecordTitles =
        {
            LocalizationService.Mark("Time trial complete."),
            LocalizationService.Mark("Run finished. Previous best remains unbeaten."),
            LocalizationService.Mark("Run complete. Better luck on the next attempt.")
        };

        public static readonly string[] TimeTrialRecordCaptions =
        {
            LocalizationService.Mark("Your new result details:"),
            LocalizationService.Mark("Summary of your latest time trial run:")
        };

        public static readonly string[] TimeTrialNoRecordCaptions =
        {
            LocalizationService.Mark("Summary of this run and your previous best:"),
            LocalizationService.Mark("Your latest run did not beat the best record. Details:")
        };

        public static readonly string[] FirstPlaceLineTemplates =
        {
            LocalizationService.Mark("{0}: position {1}, time {2}."),
            LocalizationService.Mark("{0}: finished in position {1} with {2}.")
        };

        public static readonly string[] PodiumLineTemplates =
        {
            LocalizationService.Mark("{0}: position {1}, time {2}."),
            LocalizationService.Mark("{0}: crossed in position {1} after {2}.")
        };

        public static readonly string[] FieldLineTemplates =
        {
            LocalizationService.Mark("{0}: position {1}, time {2}."),
            LocalizationService.Mark("{0}: completed in position {1} with a time of {2}.")
        };

        public static readonly string[] CrashOncePrefixes =
        {
            LocalizationService.Mark("You crashed once."),
            LocalizationService.Mark("One crash.")
        };

        public static readonly string[] CrashPluralPrefixes =
        {
            LocalizationService.Mark("You crashed {0} times."),
            LocalizationService.Mark("{0} crashes.")
        };

        public static readonly string[] CrashOnceRemarks =
        {
            LocalizationService.Mark("At least it only happened once."),
            LocalizationService.Mark("You gave the wall a quick hello and moved on."),
            LocalizationService.Mark("That was not elegant, but it was only one mistake."),
            LocalizationService.Mark("One crash is still too many, but it could be worse."),
            LocalizationService.Mark("The car flinched, and so should you."),
            LocalizationService.Mark("Consider that your one free mistake for the session."),
            LocalizationService.Mark("Not a clean run, but still recoverable."),
            LocalizationService.Mark("You made it through with one clear mistake and no excuse."),
            LocalizationService.Mark("That was one hit too many for a good lap.")
        };

        public static readonly string[] CrashFewRemarks =
        {
            LocalizationService.Mark("That was already more wall than race."),
            LocalizationService.Mark("Not ideal, but still salvageable."),
            LocalizationService.Mark("The barriers noticed you early."),
            LocalizationService.Mark("You were supposed to follow the road, not sample it repeatedly."),
            LocalizationService.Mark("A cleaner lap would have been a much better choice."),
            LocalizationService.Mark("That was enough crashing to make the suspension nervous."),
            LocalizationService.Mark("The track gave you several chances, and you used them creatively."),
            LocalizationService.Mark("You were driving like the corners had something against you."),
            LocalizationService.Mark("You kept finding the wrong line and proving it."),
            LocalizationService.Mark("That many hits should have taught you something by now."),
            LocalizationService.Mark("The car was trying to race. You were trying something else.")
        };

        public static readonly string[] CrashSeveralRemarks =
        {
            LocalizationService.Mark("The barriers are starting to recognize you."),
            LocalizationService.Mark("That was a suspicious amount of impact testing."),
            LocalizationService.Mark("You spent a lot of time negotiating with walls and losing."),
            LocalizationService.Mark("That was well past a small mistake and into a pattern."),
            LocalizationService.Mark("At this rate the crash sound is becoming your theme music."),
            LocalizationService.Mark("The car finished the race despite your repeated attempts to stop it."),
            LocalizationService.Mark("That much contact should come with a repair bill."),
            LocalizationService.Mark("You were driving like the brakes were optional."),
            LocalizationService.Mark("That was too much crashing for anyone to call it bad luck."),
        };

        public static readonly string[] CrashManyRemarks =
        {
            LocalizationService.Mark("That was less racing and more recurring demolition."),
            LocalizationService.Mark("The car deserved a calmer driver than that."),
            LocalizationService.Mark("You treated the entire event like an extended crash montage."),
            LocalizationService.Mark("The mechanics are going to need a very long evening."),
            LocalizationService.Mark("There was commitment, just not to staying on the road."),
            LocalizationService.Mark("At that point you were collecting crashes, not corners."),
            LocalizationService.Mark("You spent more time recovering than driving cleanly.")
        };

        public static readonly string[] CrashDisasterRemarks =
        {
            LocalizationService.Mark("Seriously? You could have done much better than that."),
            LocalizationService.Mark("At that point, everyone could hear the mistakes."),
            LocalizationService.Mark("That was not a clean race. That was one long damage report."),
            LocalizationService.Mark("You fought the course all the way through and still lost."),
            LocalizationService.Mark("Even the walls were probably tired of this by the end."),
            LocalizationService.Mark("That crash count takes real effort, and not the good kind.")
        };

        public static readonly string[] TimeTrialCurrentLineTemplates =
        {
            LocalizationService.Mark("Your time: {0}."),
            LocalizationService.Mark("You finished in {0}.")
        };

        public static readonly string[] TimeTrialBestRunLineTemplates =
        {
            LocalizationService.Mark("Best time so far: {0}."),
            LocalizationService.Mark("Best recorded run: {0}.")
        };

        public static readonly string[] TimeTrialAverageRunLineTemplates =
        {
            LocalizationService.Mark("Average {0}-lap time for this track: {1}."),
            LocalizationService.Mark("Average time for this track over {0} laps: {1}.")
        };

        public static readonly string[] TimeTrialLapSummaryTitles =
        {
            LocalizationService.Mark("Lap summary:"),
            LocalizationService.Mark("Lap statistics:")
        };

        public static readonly string[] TimeTrialRunBestLapLineTemplates =
        {
            LocalizationService.Mark("Best lap this run: {0}."),
            LocalizationService.Mark("Your quickest lap this run: {0}.")
        };

        public static readonly string[] TimeTrialBestLapLineTemplates =
        {
            LocalizationService.Mark("Best lap for this track: {0}."),
            LocalizationService.Mark("Fastest recorded lap on this track: {0}.")
        };

        public static readonly string[] TimeTrialAverageLapLineTemplates =
        {
            LocalizationService.Mark("Average lap time for this track: {0}."),
            LocalizationService.Mark("Track lap average: {0}.")
        };
    }
}

