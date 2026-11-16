using System.Collections.Generic;

public struct VetAnswer
{
    public string text;
    public bool good;
    public string reply;
}

public struct VetQuestion
{
    public string text;
    public VetAnswer[] answers;
}

// Everything the vet says. "your pet" / "Your pet" is swapped for the pet's
// name when shown (PetProfile.Fill). Each line teaches one real pet-care idea.
public static class VetDialogue
{
    static VetAnswer A(string text, bool good, string reply) => new VetAnswer { text = text, good = good, reply = reply };

    static readonly VetQuestion[] CareQuestions =
    {
        new VetQuestion { text = "Tell me, how often do you feed your pet?", answers = new[] {
            A("Small meals at set times", true, "Perfect! Regular meals at the same times keep tummies happy."),
            A("One huge bowl, all day", false, "Pets can overeat that way. Regular, measured meals are better."),
            A("Only when it begs", false, "Pets do best with meals at set times, not just when they beg.") } },
        new VetQuestion { text = "How often do you change your pet's water?", answers = new[] {
            A("Fresh water every day", true, "Great! Clean, fresh water every day keeps pets healthy."),
            A("When the bowl is empty", false, "Old water gets dirty. Try changing it every day."),
            A("About once a week", false, "That's a bit long. Fresh water every day is best.") } },
        new VetQuestion { text = "How often do you brush your pet?", answers = new[] {
            A("A few times a week", true, "Lovely! Brushing keeps the coat healthy and helps you spot fleas early."),
            A("Never, it's fine", false, "Brushing helps you spot fleas and tangles early. Try a few times a week."),
            A("Only when it's very messy", false, "Little and often is much gentler than brushing out big tangles.") } },
        new VetQuestion { text = "How much does your pet play each day?", answers = new[] {
            A("Some play every day", true, "Wonderful! Daily play keeps pets fit and happy."),
            A("Hardly ever", false, "Pets need play and exercise every day to stay healthy."),
            A("All day, no breaks", false, "Play is great, but pets need plenty of rest too!") } },
    };

    public static readonly VetQuestion VaccineQuestion = new VetQuestion
    {
        text = "Before we start: do you know why pets need vaccines?",
        answers = new[] {
            A("To protect them from serious illness", true, "Exactly! A vaccine teaches the body to fight germs before they can make your pet sick."),
            A("To help them grow bigger", false, "Not quite! Vaccines protect pets from serious illnesses."),
            A("They don't really need them", false, "They really do! Vaccines protect pets from serious illnesses."),
        }
    };

    public static VetQuestion RandomCareQuestion()
    {
        return CareQuestions[UnityEngine.Random.Range(0, CareQuestions.Length)];
    }

    // ---- Symptoms ----

    static string SymptomText(Illness illness)
    {
        switch (illness)
        {
            case Illness.Cold: return "Sneezing, runny nose";
            case Illness.Fleas: return "Scratching all the time";
            case Illness.TummyAche: return "Sore tummy, not eating";
            default: return "Shaking its head a lot";
        }
    }

    // The right symptom plus two others, shuffled.
    public static List<KeyValuePair<string, bool>> SymptomChoices(Illness illness)
    {
        var others = new List<Illness> { Illness.Cold, Illness.Fleas, Illness.TummyAche, Illness.EarInfection };
        others.Remove(illness);
        others.RemoveAt(UnityEngine.Random.Range(0, others.Count));
        var choices = new List<KeyValuePair<string, bool>> { new KeyValuePair<string, bool>(SymptomText(illness), true) };
        foreach (Illness other in others)
            choices.Add(new KeyValuePair<string, bool>(SymptomText(other), false));
        Shuffle(choices);
        return choices;
    }

    public static string SymptomHint(Illness illness)
    {
        switch (illness)
        {
            case Illness.Cold: return "keeps sneezing.";
            case Illness.Fleas: return "keeps scratching.";
            case Illness.TummyAche: return "has a sore tummy.";
            case Illness.EarInfection: return "keeps shaking its head.";
            default: return "doesn't look well.";
        }
    }

    // ---- Exam ----

    // What the vet sees at each spot of InspectInteraction (ears, eyes, teeth, fur).
    public static string Finding(int spot, Illness illness)
    {
        switch (spot)
        {
            case 0: return illness == Illness.EarInfection ? "This ear is red and sore. That explains the head shaking!" : "Ears look clean and healthy.";
            case 1: return illness == Illness.Cold ? "Eyes are a bit watery, and the nose is runny." : "Bright, clear eyes. Good!";
            case 2: return illness == Illness.TummyAche ? "Teeth are fine, but the tummy feels tender." : "Nice clean teeth!";
            default: return illness == Illness.Fleas ? "Aha! I can see tiny fleas hopping in the fur." : "Soft, shiny fur. Lovely.";
        }
    }

    public static string Diagnosis(Illness illness)
    {
        switch (illness)
        {
            case Illness.Cold: return "Your pet has a little cold. Pets catch colds when they're tired and run down. A spoonful of medicine will help.";
            case Illness.Fleas: return "Your pet has fleas! They're itchy little bugs that love messy fur. Let's comb them all out.";
            case Illness.TummyAche: return "Your pet has an upset tummy, probably from skipped meals or water. A gentle tummy rub will soothe it.";
            default: return "Your pet has a mild ear infection. Dirt can make ears sore. Let's put in some ear drops.";
        }
    }

    public static string AfterCareAdvice(Illness illness)
    {
        switch (illness)
        {
            case Illness.Cold: return "At home, let your pet rest and give it fresh water.";
            case Illness.Fleas: return "At home, give your pet a bath and brush its fur to get the last fleas out.";
            case Illness.TummyAche: return "At home, give your pet a small meal and fresh water.";
            default: return "At home, brush your pet gently and let it rest.";
        }
    }

    // A healthy pet: one tip based on its lowest need.
    public static string HealthyAdvice(PetNeeds needs)
    {
        Need? low = needs != null ? needs.MostPressing() : null;
        if (low == null)
            return "Keep up the great care!";
        switch (low.Value)
        {
            case Need.Hunger: return "It looks a little hungry, though. Regular meals keep pets healthy.";
            case Need.Thirst: return "It's a bit thirsty. Keep the water bowl full and fresh.";
            case Need.Cleanliness: return "A bath soon will keep its skin and fur healthy, and fleas away.";
            case Need.Energy: return "It seems tired. Make sure it gets plenty of rest.";
            default: return "Lots of play and cuddles keep pets happy, and happy pets stay healthier.";
        }
    }

    public static string StepList(AfterCare steps)
    {
        var names = new List<string>();
        if ((steps & AfterCare.Feed) != 0) names.Add("Feed");
        if ((steps & AfterCare.Water) != 0) names.Add("Water");
        if ((steps & AfterCare.Bath) != 0) names.Add("Bath");
        if ((steps & AfterCare.Brush) != 0) names.Add("Groom");
        if ((steps & AfterCare.Rest) != 0) names.Add("Rest");
        return string.Join(" + ", names);
    }

    public static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T t = list[i];
            list[i] = list[j];
            list[j] = t;
        }
    }
}
