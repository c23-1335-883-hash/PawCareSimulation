using System;
using UnityEngine;

// Small icons the speech bubble can show next to a line (the font has no emoji).
public enum LineIcon
{
    None,
    Heart,
    Ball,
    Moon,
    Sun,
    Food,
    Water,
    Bubbles,
    Paw,
    Star
}

public struct Line
{
    public string text;
    public LineIcon icon;

    public Line(string text, LineIcon icon = LineIcon.None)
    {
        this.text = text;
        this.icon = icon;
    }
}

// Everything the pet says. Dogs and cats have their own lines, and the
// choice depends on mood, time of day, personality and time spent away.
public static class PetLines
{
    static Line L(string text, LineIcon icon = LineIcon.None) => new Line(text, icon);

    static Line Pick(Line[] lines) => lines[UnityEngine.Random.Range(0, lines.Length)];

    static Line Pick(bool cat, Line[] dog, Line[] catLines) => Pick(cat ? catLines : dog);

    public enum DayPart { Morning, Afternoon, Evening, Night }

    public static DayPart Now
    {
        get
        {
            int h = DateTime.Now.Hour;
            if (h >= 5 && h < 12) return DayPart.Morning;
            if (h >= 12 && h < 18) return DayPart.Afternoon;
            if (h >= 18 && h < 22) return DayPart.Evening;
            return DayPart.Night;
        }
    }

    // ---- Hello ----

    public static Line Greeting(bool cat, float minutesAway)
    {
        if (minutesAway >= 60f * 20f)
            return cat ? L("You're back! I missed you... a little.", LineIcon.Heart) : L("You're back! I missed you SO much!", LineIcon.Heart);
        if (minutesAway >= 60f)
            return cat ? L("Oh, it's you. Purr... hi!", LineIcon.Heart) : L("Yay, you're home!", LineIcon.Heart);

        switch (Now)
        {
            case DayPart.Morning: return cat ? L("Good morning! Breakfast time?", LineIcon.Sun) : L("Good morning! Woof!", LineIcon.Sun);
            case DayPart.Afternoon: return cat ? L("Mrrp! Good afternoon!", LineIcon.Sun) : L("Hi hi hi! Let's have fun!", LineIcon.Paw);
            case DayPart.Evening: return cat ? L("Good evening... purr.", LineIcon.Star) : L("Good evening, friend!", LineIcon.Star);
            default: return cat ? L("A night owl, like me!", LineIcon.Moon) : L("It's late... cuddles?", LineIcon.Moon);
        }
    }

    // ---- Needs ----

    public static Line Reminder(bool cat, Need need)
    {
        switch (need)
        {
            case Need.Hunger: return Pick(cat,
                new[] { L("My tummy is rumbling...", LineIcon.Food), L("Is it dinner time yet?", LineIcon.Food) },
                new[] { L("Meow! Food, please!", LineIcon.Food), L("My bowl is empty...", LineIcon.Food) });
            case Need.Thirst: return Pick(cat,
                new[] { L("Can I have some water?", LineIcon.Water), L("*pant pant* So thirsty!", LineIcon.Water) },
                new[] { L("Fresh water, please?", LineIcon.Water), L("Mew... I'm thirsty.", LineIcon.Water) });
            case Need.Cleanliness: return Pick(cat,
                new[] { L("I rolled in something...", LineIcon.Bubbles), L("Bath time? Maybe?", LineIcon.Bubbles) },
                new[] { L("My fur is all messy!", LineIcon.Bubbles), L("Help me get clean?", LineIcon.Bubbles) });
            case Need.Energy: return Pick(cat,
                new[] { L("I'm sleepy...", LineIcon.Moon), L("*yawn* Nap time?", LineIcon.Moon) },
                new[] { L("Time for a cat nap...", LineIcon.Moon), L("*yawn* So sleepy...", LineIcon.Moon) });
            default: return Pick(cat,
                new[] { L("Can we play?", LineIcon.Ball), L("I feel a bit lonely...", LineIcon.Heart) },
                new[] { L("Play with me?", LineIcon.Ball), L("Pay attention to me!", LineIcon.Heart) });
        }
    }

    // ---- Feeling sick (PetHealth) ----

    public static Line Symptom(bool cat, Illness illness)
    {
        switch (illness)
        {
            case Illness.Cold: return Pick(cat,
                new[] { L("Achoo! I don't feel so good..."), L("*sniffle* My nose is all runny...") },
                new[] { L("Achoo! Mew..."), L("*sniff* I feel all stuffy...") });
            case Illness.Fleas: return Pick(cat,
                new[] { L("Itchy, itchy, itchy!"), L("Something keeps biting me!") },
                new[] { L("Why am I so itchy?!"), L("Hss! Something's in my fur!") });
            case Illness.TummyAche: return Pick(cat,
                new[] { L("My tummy hurts...", LineIcon.Food), L("Ugh... I don't feel hungry.") },
                new[] { L("Mrrow... my tummy aches.", LineIcon.Food), L("I don't want food right now...") });
            default: return Pick(cat,
                new[] { L("My ear feels funny..."), L("*shakes head* Ow, my ear!") },
                new[] { L("My ear is all itchy..."), L("Mew! My ear hurts...") });
        }
    }

    // ---- Chatter when nothing is urgent ----

    public static Line Chatter(bool cat, Mood mood, Trait trait)
    {
        if (mood == Mood.Sad)
            return cat ? L("Mew... a cuddle would help.", LineIcon.Heart) : L("Will you play with me?", LineIcon.Heart);

        // Sometimes say something about the personality.
        if (UnityEngine.Random.value < 0.3f)
        {
            switch (trait)
            {
                case Trait.Playful: return cat ? L("Let's chase something!", LineIcon.Ball) : L("Can we play? Please?", LineIcon.Ball);
                case Trait.Lazy: return cat ? L("Sunbeam nap later?", LineIcon.Moon) : L("Naps are the best...", LineIcon.Moon);
                case Trait.Foodie: return cat ? L("I dream of fish...", LineIcon.Food) : L("Do you smell treats?", LineIcon.Food);
                case Trait.Clingy: return cat ? L("Stay right here, okay?", LineIcon.Heart) : L("I love you lots!", LineIcon.Heart);
                default: return cat ? L("...hi. *hides*", LineIcon.Paw) : L("Um... hi there.", LineIcon.Paw);
            }
        }

        switch (Now)
        {
            case DayPart.Morning: return Pick(cat,
                new[] { L("What a sunny day!", LineIcon.Sun), L("Morning walkies?", LineIcon.Paw) },
                new[] { L("The sun is so warm...", LineIcon.Sun), L("Purr... cosy morning.", LineIcon.Sun) });
            case DayPart.Night: return Pick(cat,
                new[] { L("Look, the stars!", LineIcon.Star), L("Sleepy time soon...", LineIcon.Moon) },
                new[] { L("I see in the dark!", LineIcon.Star), L("The moon is pretty.", LineIcon.Moon) });
            default: return Pick(cat,
                new[] { L("I'm so happy!", LineIcon.Heart), L("Woof! Best day ever!", LineIcon.Paw), L("You're my best friend!", LineIcon.Heart) },
                new[] { L("Purrrr...", LineIcon.Heart), L("This is my spot now.", LineIcon.Paw), L("You may pet me.", LineIcon.Heart) });
        }
    }

    // ---- Reactions ----

    public static Line Petting(bool cat, Need? pressing, bool loves)
    {
        if (pressing == Need.Hunger) return L("Thanks! I'm a bit hungry, though.", LineIcon.Food);
        if (pressing == Need.Thirst) return L("Thanks! Could I have some water?", LineIcon.Water);
        if (pressing == Need.Cleanliness) return L("Hehe! I could use a bath.", LineIcon.Bubbles);
        if (pressing == Need.Energy) return L("Mmm... I'm getting sleepy.", LineIcon.Moon);
        if (loves)
            return cat ? L("Purrrrr! More, more!", LineIcon.Heart) : L("I LOVE cuddles!", LineIcon.Heart);
        return Pick(cat,
            new[] { L("That feels nice!", LineIcon.Heart), L("I love you too!", LineIcon.Heart), L("Hehe, that tickles!", LineIcon.Paw) },
            new[] { L("Purr... nice.", LineIcon.Heart), L("Yes, right there!", LineIcon.Heart), L("*happy purring*", LineIcon.Heart) });
    }

    public static Line Loved(bool cat, CareActivity activity)
    {
        switch (activity)
        {
            case CareActivity.Feed: return cat ? L("Best. Meal. Ever!", LineIcon.Food) : L("Yum yum YUM!", LineIcon.Food);
            case CareActivity.Play: return cat ? L("Again! Again!", LineIcon.Ball) : L("That was AMAZING!", LineIcon.Ball);
            case CareActivity.Groom: return cat ? L("Purr... so soft.", LineIcon.Star) : L("I feel so pretty!", LineIcon.Star);
            default: return L("I love this!", LineIcon.Heart);
        }
    }

    public static Line Woke(bool cat, bool loves)
    {
        if (loves)
            return cat ? L("Best nap ever...", LineIcon.Moon) : L("Ahh, I love naps!", LineIcon.Moon);
        return cat ? L("*stretch* Good nap.", LineIcon.Sun) : L("Good morning!", LineIcon.Sun);
    }

    public static Line Yawn(bool cat) => cat ? L("*yaaawn* mrrp...", LineIcon.Moon) : L("*yaaawn*", LineIcon.Moon);

    public static Line Rumble => L("*grumble grumble*", LineIcon.Food);
}
