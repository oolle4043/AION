using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal static class Ko
{
    public const string Main = "\uBCF8";
    public const string Alt = "\uBD80";
    public const string Templar = "\uC218\uD638\uC131";
    public const string Gladiator = "\uAC80\uC131";
    public const string Assassin = "\uC0B4\uC131";
    public const string Chanter = "\uD638\uBC95\uC131";
    public const string Cleric = "\uCE58\uC720\uC131";
    public const string Sorcerer = "\uB9C8\uB3C4\uC131";
    public const string Spiritmaster = "\uC815\uB839\uC131";
    public const string Ranger = "\uAD81\uC131";
    public const string Raid = "\uACF5\uB300";
    public const string Party = "\uD30C\uD2F0";
    public const string AveragePower = "\uD3C9\uADE0\uC804\uD22C\uB825";
}

internal static class PartyGenerator
{
    public static PartyGenerationResult Generate(string listPath, PartyGenerationOptions? options = null)
    {
        options ??= PartyGenerationOptions.Default;
        options.Validate();

        string fullListPath = Path.GetFullPath(listPath);
        if (!File.Exists(fullListPath))
        {
            throw new FileNotFoundException("list.txt file was not found.", fullListPath);
        }

        string resultPath = Path.Combine(Path.GetDirectoryName(fullListPath) ?? AppContext.BaseDirectory, "party_result.txt");
        IReadOnlyList<UserRoster> users = PartyInputParser.ReadUsers(fullListPath, options.AltCountPerParty);
        PartyOptimizer optimizer = new(users, options);
        PartyPlan plan = optimizer.BuildBestPlan();

        string report = PartyReportWriter.Write(plan);
        File.WriteAllText(resultPath, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new PartyGenerationResult(fullListPath, resultPath, report);
    }
}

internal sealed record PartyGenerationOptions(int MainCountPerParty, int AltCountPerParty)
{
    public static PartyGenerationOptions Default { get; } = new(MainCountPerParty: 1, AltCountPerParty: 3);
    public int PartySize => MainCountPerParty + AltCountPerParty;

    public void Validate()
    {
        if (MainCountPerParty != 1)
        {
            throw new InvalidOperationException("Current optimizer supports main count 1 only.");
        }

        if (AltCountPerParty is < 1 or > 3)
        {
            throw new InvalidOperationException("Alt count must be between 1 and 3.");
        }
    }
}

internal sealed record PartyGenerationResult(string ListPath, string ResultPath, string Report);

internal sealed class PartyOptimizer
{
    private const int RaidCount = 4;
    private const int PartyCountPerRaid = 2;
    private const int CandidateCount = 35_000;

    private static readonly HashSet<string> Party2MainPreferredJobs = new(StringComparer.Ordinal)
    {
        Ko.Gladiator, Ko.Templar, Ko.Chanter, Ko.Assassin
    };

    private static readonly HashSet<string> SpecialMainJobs = new(StringComparer.Ordinal)
    {
        Ko.Cleric, Ko.Chanter
    };

    private readonly IReadOnlyList<UserRoster> _users;
    private readonly PartyGenerationOptions _options;
    private readonly Random _random = new(20260507);

    public PartyOptimizer(IReadOnlyList<UserRoster> users, PartyGenerationOptions options)
    {
        _users = users;
        _options = options;
    }

    public PartyPlan BuildBestPlan()
    {
        PartyPlan? best = null;
        double bestScore = double.MaxValue;

        for (int i = 0; i < CandidateCount; i++)
        {
            PartyPlan candidate = BuildCandidate(i);
            double score = Score(candidate);

            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best ?? throw new InvalidOperationException("Could not build a party plan.");
    }

    private PartyPlan BuildCandidate(int iteration)
    {
        PartyPlan plan = PartyPlan.CreateEmpty(_options);
        AssignMains(plan, iteration);
        AssignAltsToRaids(plan, iteration);
        SplitRaidAltsIntoParties(plan);
        plan.RefreshWarnings();
        return plan;
    }

    private void AssignMains(PartyPlan plan, int iteration)
    {
        List<int> users = Enumerable.Range(0, _users.Count).ToList();
        Shuffle(users);

        List<PartySlot> party2Slots = Enumerable.Range(0, RaidCount)
            .Select(raidIndex => new PartySlot(raidIndex, PartyIndex: 1))
            .ToList();
        Shuffle(party2Slots);

        List<int> preferredMainUsers = users
            .Where(userIndex => Party2MainPreferredJobs.Contains(_users[userIndex].Main.Job))
            .ToList();
        Shuffle(preferredMainUsers);

        HashSet<int> assignedUsers = new();
        int preferredCount = Math.Min(party2Slots.Count, preferredMainUsers.Count);
        for (int i = 0; i < preferredCount; i++)
        {
            AddMain(plan, party2Slots[i], preferredMainUsers[i]);
            assignedUsers.Add(preferredMainUsers[i]);
        }

        List<PartySlot> remainingSlots = AllPartySlots()
            .Where(slot => plan.Parties[slot.RaidIndex][slot.PartyIndex].Members.Count == 0)
            .ToList();
        Shuffle(remainingSlots);

        List<int> remainingUsers = users
            .Where(userIndex => !assignedUsers.Contains(userIndex))
            .ToList();

        if (iteration % 5 == 0)
        {
            remainingUsers = remainingUsers
                .OrderByDescending(userIndex => _users[userIndex].Main.Power)
                .ToList();
        }

        for (int i = 0; i < remainingUsers.Count; i++)
        {
            AddMain(plan, remainingSlots[i], remainingUsers[i]);
        }
    }

    private void AddMain(PartyPlan plan, PartySlot slot, int userIndex)
    {
        CharacterEntry main = _users[userIndex].Main;
        plan.Parties[slot.RaidIndex][slot.PartyIndex].Members.Add(main);
        plan.MainRaidByUser[userIndex] = slot.RaidIndex;
        plan.RaidUserSets[slot.RaidIndex].Add(userIndex);
    }

    private void AssignAltsToRaids(PartyPlan plan, int iteration)
    {
        int raidAltCapacity = PartyCountPerRaid * _options.AltCountPerParty;
        Dictionary<int, Queue<CharacterEntry>> userAlts = _users.ToDictionary(
            user => user.UserIndex,
            user =>
            {
                IEnumerable<CharacterEntry> selected = user.Alts
                    .OrderByDescending(alt => alt.Power)
                    .Take(_options.AltCountPerParty);

                if (iteration % 3 != 0)
                {
                    selected = selected.OrderBy(_ => _random.Next());
                }

                return new Queue<CharacterEntry>(selected);
            });

        List<int> userOrder = userAlts.Keys.ToList();
        Shuffle(userOrder);

        foreach (int userIndex in userOrder)
        {
            while (userAlts[userIndex].Count > 0)
            {
                CharacterEntry alt = userAlts[userIndex].Dequeue();
                List<int> candidates = Enumerable.Range(0, RaidCount)
                    .Where(raidIndex => raidIndex != plan.MainRaidByUser[userIndex])
                    .Where(raidIndex => !plan.RaidUserSets[raidIndex].Contains(userIndex))
                    .Where(raidIndex => plan.RaidAltPool[raidIndex].Count < raidAltCapacity)
                    .OrderBy(raidIndex => plan.RaidAltPool[raidIndex].Sum(x => x.Power))
                    .ThenBy(_ => _random.Next())
                    .ToList();

                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException("Could not assign alts without same-user raid duplication.");
                }

                int selectedRaid = candidates[0];
                plan.RaidAltPool[selectedRaid].Add(alt);
                plan.RaidUserSets[selectedRaid].Add(userIndex);
            }
        }
    }

    private void SplitRaidAltsIntoParties(PartyPlan plan)
    {
        for (int raidIndex = 0; raidIndex < RaidCount; raidIndex++)
        {
            List<CharacterEntry> alts = plan.RaidAltPool[raidIndex];
            IReadOnlyList<CharacterEntry> p1Base = plan.Parties[raidIndex][0].Members;
            IReadOnlyList<CharacterEntry> p2Base = plan.Parties[raidIndex][1].Members;

            List<CharacterEntry>? bestP1Alts = null;
            List<CharacterEntry>? bestP2Alts = null;
            double bestScore = double.MaxValue;

            foreach (List<CharacterEntry> p1Alts in Choose(alts, _options.AltCountPerParty))
            {
                List<CharacterEntry> p2Alts = alts.Except(p1Alts).ToList();
                double score = ScoreRaidSplit(p1Base, p2Base, p1Alts, p2Alts);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestP1Alts = p1Alts;
                    bestP2Alts = p2Alts;
                }
            }

            plan.Parties[raidIndex][0].Members.AddRange(bestP1Alts ?? throw new InvalidOperationException());
            plan.Parties[raidIndex][1].Members.AddRange(bestP2Alts ?? throw new InvalidOperationException());
        }
    }

    private static double ScoreRaidSplit(
        IReadOnlyList<CharacterEntry> p1Base,
        IReadOnlyList<CharacterEntry> p2Base,
        IReadOnlyList<CharacterEntry> p1Alts,
        IReadOnlyList<CharacterEntry> p2Alts)
    {
        List<CharacterEntry> p1 = p1Base.Concat(p1Alts).ToList();
        List<CharacterEntry> p2 = p2Base.Concat(p2Alts).ToList();

        double p1Average = p1.Average(x => x.Power);
        double p2Average = p2.Average(x => x.Power);
        double score = Math.Abs(p1Average - p2Average) * 2.2;

        score += SupportPenalty(p2) / 50.0;
        score += SpecialMainStrongAltReward(p1);
        score += SpecialMainStrongAltReward(p2);

        return score;
    }

    private double Score(PartyPlan plan)
    {
        List<PartyGroup> parties = plan.Parties.SelectMany(x => x).ToList();
        double[] averages = parties.Select(x => x.AveragePower).ToArray();
        double totalAverage = averages.Average();

        double variance = averages.Sum(avg => Math.Pow(avg - totalAverage, 2)) / averages.Length;
        double range = averages.Max() - averages.Min();
        double raidDiff = plan.Parties.Sum(raid => Math.Abs(raid[0].AveragePower - raid[1].AveragePower));
        double supportPenalty = parties
            .Where(x => x.PartyIndex == 1)
            .Sum(x => SupportPenalty(x.Members));
        double p2MainPenalty = plan.Parties
            .Select(raid => raid[1].Members.FirstOrDefault(x => x.IsMain))
            .Count(main => main is not null && !Party2MainPreferredJobs.Contains(main.Job)) * 200_000.0;
        double specialMainStrongAltReward = parties.Sum(party => SpecialMainStrongAltReward(party.Members));
        double duplicatePenalty = ConstraintPenalty(plan);

        return variance / 1_000.0
            + range * 4.0
            + raidDiff * 1.4
            + supportPenalty
            + p2MainPenalty
            + specialMainStrongAltReward
            + duplicatePenalty;
    }

    private static double SupportPenalty(IReadOnlyList<CharacterEntry> members)
    {
        if (members.Any(x => x.Job == Ko.Cleric))
        {
            return 0;
        }

        if (members.Any(x => x.Job == Ko.Chanter))
        {
            return 5_000;
        }

        return 5_000_000;
    }

    private static double SpecialMainStrongAltReward(IReadOnlyList<CharacterEntry> members)
    {
        CharacterEntry? main = members.FirstOrDefault(x => x.IsMain);
        if (main is null || !SpecialMainJobs.Contains(main.Job))
        {
            return 0;
        }

        double weightedDamagePower = members
            .Where(x => !x.IsMain)
            .Sum(x => x.Power * GetDamageWeight(x.Job));

        double lowDamagePenalty = members
            .Where(x => !x.IsMain)
            .Sum(x => (1.0 - GetDamageWeight(x.Job)) * x.Power * 0.04);

        return -weightedDamagePower * 0.2 + lowDamagePenalty;
    }

    private static double GetDamageWeight(string job)
    {
        if (job == Ko.Assassin)
        {
            return 1.00;
        }

        if (job == Ko.Sorcerer)
        {
            return 0.92;
        }

        if (job == Ko.Spiritmaster)
        {
            return 0.92;
        }

        if (job == Ko.Ranger)
        {
            return 0.92;
        }

        if (job == Ko.Templar)
        {
            return 0.72;
        }

        if (job == Ko.Gladiator)
        {
            return 0.72;
        }

        if (job == Ko.Chanter)
        {
            return 0.25;
        }

        if (job == Ko.Cleric)
        {
            return 0.25;
        }

        return 0.7;
    }

    private double ConstraintPenalty(PartyPlan plan)
    {
        double penalty = 0;

        foreach (PartyGroup[] raid in plan.Parties)
        {
            List<CharacterEntry> members = raid.SelectMany(x => x.Members).ToList();
            penalty += Math.Max(0, members.Count - members.Select(x => x.UserIndex).Distinct().Count()) * 10_000_000;
        }

        List<CharacterEntry> allMembers = plan.Parties.SelectMany(x => x).SelectMany(x => x.Members).ToList();
        penalty += Math.Max(0, allMembers.Count - allMembers.Select(x => x.UniqueKey).Distinct().Count()) * 10_000_000;

        foreach (PartyGroup party in plan.Parties.SelectMany(x => x))
        {
            penalty += Math.Abs(_options.PartySize - party.Members.Count) * 10_000_000;
            penalty += Math.Abs(1 - party.Members.Count(x => x.IsMain)) * 10_000_000;
        }

        return penalty;
    }

    private IEnumerable<PartySlot> AllPartySlots()
    {
        for (int raidIndex = 0; raidIndex < RaidCount; raidIndex++)
        {
            for (int partyIndex = 0; partyIndex < PartyCountPerRaid; partyIndex++)
            {
                yield return new PartySlot(raidIndex, partyIndex);
            }
        }
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static IEnumerable<List<T>> Choose<T>(IReadOnlyList<T> values, int count)
    {
        if (count < 0 || count > values.Count)
        {
            yield break;
        }

        int[] indexes = Enumerable.Range(0, count).ToArray();

        while (true)
        {
            yield return indexes.Select(index => values[index]).ToList();

            int i;
            for (i = count - 1; i >= 0; i--)
            {
                if (indexes[i] != i + values.Count - count)
                {
                    break;
                }
            }

            if (i < 0)
            {
                yield break;
            }

            indexes[i]++;
            for (int j = i + 1; j < count; j++)
            {
                indexes[j] = indexes[j - 1] + 1;
            }
        }
    }
}

internal static class PartyReportWriter
{
    private const int LeftColumnWidth = 42;

    public static string Write(PartyPlan plan)
    {
        StringBuilder builder = new();

        foreach (string warning in plan.Warnings)
        {
            builder.AppendLine(warning);
        }

        if (plan.Warnings.Count > 0)
        {
            builder.AppendLine();
        }

        for (int raidIndex = 0; raidIndex < plan.Parties.Length; raidIndex++)
        {
            PartyGroup p1 = plan.Parties[raidIndex][0];
            PartyGroup p2 = plan.Parties[raidIndex][1];

            builder.AppendLine($"{raidIndex + 1}{Ko.Raid}");
            builder.AppendLine($"{PadDisplay($"1{Ko.Party}", LeftColumnWidth)}2{Ko.Party}");

            int rowCount = Math.Max(p1.Members.Count, p2.Members.Count);
            for (int memberIndex = 0; memberIndex < rowCount; memberIndex++)
            {
                string left = memberIndex < p1.Members.Count ? p1.Members[memberIndex].Format() : "";
                string right = memberIndex < p2.Members.Count ? p2.Members[memberIndex].Format() : "";
                builder.AppendLine($"{PadDisplay(left, LeftColumnWidth)}{right}");
            }

            string p1Average = $"1{Ko.Party} {Ko.AveragePower}: {p1.AveragePower:N0}";
            string p2Average = $"2{Ko.Party} {Ko.AveragePower}: {p2.AveragePower:N0}";
            builder.AppendLine($"{PadDisplay(p1Average, LeftColumnWidth)}{p2Average}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string PadDisplay(string text, int width)
    {
        int displayWidth = text.Sum(GetDisplayWidth);
        return displayWidth >= width
            ? text + "  "
            : text + new string(' ', width - displayWidth);
    }

    private static int GetDisplayWidth(char value)
    {
        return value <= 0x007F ? 1 : 2;
    }
}

internal static partial class PartyInputParser
{
    private static readonly Regex CharacterPattern = CharacterLineRegex();
    private static readonly HashSet<string> AllowedJobs = new(StringComparer.Ordinal)
    {
        Ko.Templar, Ko.Gladiator, Ko.Assassin, Ko.Chanter,
        Ko.Cleric, Ko.Sorcerer, Ko.Spiritmaster, Ko.Ranger
    };

    public static IReadOnlyList<UserRoster> ReadUsers(string listPath, int requiredAltCount)
    {
        string[] lines = File.ReadAllLines(listPath, Encoding.UTF8)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToArray();

        if (lines.Length != 8)
        {
            throw new InvalidDataException($"list.txt must contain exactly 8 lines. Current line count: {lines.Length}");
        }

        List<UserRoster> users = new();
        for (int i = 0; i < lines.Length; i++)
        {
            users.Add(ParseUserLine(i, lines[i], requiredAltCount));
        }

        return users;
    }

    private static UserRoster ParseUserLine(int userIndex, string line, int requiredAltCount)
    {
        string[] parts = line.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1 + requiredAltCount)
        {
            throw new InvalidDataException($"Line {userIndex + 1} needs one main and at least {requiredAltCount} alts.");
        }

        List<CharacterEntry> characters = new();
        for (int i = 0; i < parts.Length; i++)
        {
            characters.Add(ParseCharacter(userIndex, i, parts[i]));
        }

        CharacterEntry? main = characters.SingleOrDefault(x => x.IsMain);
        if (main is null)
        {
            throw new InvalidDataException($"Line {userIndex + 1} must contain exactly one main character.");
        }

        List<CharacterEntry> alts = characters.Where(x => !x.IsMain).ToList();
        if (alts.Count < requiredAltCount)
        {
            throw new InvalidDataException($"Line {userIndex + 1} must contain at least {requiredAltCount} alt characters.");
        }

        return new UserRoster(userIndex, main, alts);
    }

    private static CharacterEntry ParseCharacter(int userIndex, int characterIndex, string text)
    {
        Match match = CharacterPattern.Match(text);
        if (!match.Success)
        {
            throw new InvalidDataException($"Invalid character format on line {userIndex + 1}, item {characterIndex + 1}: {text}");
        }

        string name = match.Groups["name"].Value.Trim();
        string kind = match.Groups["kind"].Value.Trim();
        string job = match.Groups["job"].Value.Trim();
        string powerText = match.Groups["power"].Value.Replace(",", "").Trim();

        if (!AllowedJobs.Contains(job))
        {
            throw new InvalidDataException($"Invalid job for {name}: {job}");
        }

        if (!long.TryParse(powerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long power))
        {
            throw new InvalidDataException($"Invalid power for {name}: {match.Groups["power"].Value}");
        }

        return new CharacterEntry(userIndex, characterIndex, name, kind, job, power);
    }

    [GeneratedRegex(@"^(?<name>.+?)\((?<kind>\uBCF8|\uBD80)\s*,\s*(?<job>[^,]+)\s*,\s*(?<power>[\d,]+)\)$")]
    private static partial Regex CharacterLineRegex();
}

internal sealed class PartyPlan
{
    public PartyGroup[][] Parties { get; }
    public List<CharacterEntry>[] RaidAltPool { get; }
    public HashSet<int>[] RaidUserSets { get; }
    public int[] MainRaidByUser { get; }
    public List<string> Warnings { get; } = new();

    private PartyPlan(
        PartyGroup[][] parties,
        List<CharacterEntry>[] raidAltPool,
        HashSet<int>[] raidUserSets,
        int[] mainRaidByUser)
    {
        Parties = parties;
        RaidAltPool = raidAltPool;
        RaidUserSets = raidUserSets;
        MainRaidByUser = mainRaidByUser;
    }

    public static PartyPlan CreateEmpty(PartyGenerationOptions options)
    {
        PartyGroup[][] parties = Enumerable.Range(0, 4)
            .Select(raidIndex => Enumerable.Range(0, 2)
                .Select(partyIndex => new PartyGroup(raidIndex, partyIndex))
                .ToArray())
            .ToArray();

        List<CharacterEntry>[] raidAltPool = Enumerable.Range(0, 4)
            .Select(_ => new List<CharacterEntry>(2 * options.AltCountPerParty))
            .ToArray();

        HashSet<int>[] raidUserSets = Enumerable.Range(0, 4)
            .Select(_ => new HashSet<int>())
            .ToArray();

        return new PartyPlan(parties, raidAltPool, raidUserSets, Enumerable.Repeat(-1, 8).ToArray());
    }

    public void RefreshWarnings()
    {
        Warnings.Clear();

        foreach (PartyGroup party in Parties.Select(raid => raid[1]))
        {
            bool hasCleric = party.Members.Any(x => x.Job == Ko.Cleric);
            bool hasChanter = party.Members.Any(x => x.Job == Ko.Chanter);

            if (!hasCleric && !hasChanter)
            {
                string members = string.Join(" / ", party.Members.Select(x => x.Format()));
                Warnings.Add($"Warning: {party.RaidIndex + 1}{Ko.Raid} 2{Ko.Party} has no {Ko.Cleric} or {Ko.Chanter}. Best available party: {members}");
            }
        }
    }
}

internal sealed class PartyGroup
{
    public int RaidIndex { get; }
    public int PartyIndex { get; }
    public List<CharacterEntry> Members { get; } = new();
    public double AveragePower => Members.Count == 0 ? 0 : Members.Average(x => x.Power);

    public PartyGroup(int raidIndex, int partyIndex)
    {
        RaidIndex = raidIndex;
        PartyIndex = partyIndex;
    }
}

internal sealed record UserRoster(int UserIndex, CharacterEntry Main, IReadOnlyList<CharacterEntry> Alts);

internal sealed record CharacterEntry(
    int UserIndex,
    int CharacterIndex,
    string Name,
    string Kind,
    string Job,
    long Power)
{
    public bool IsMain => Kind == Ko.Main;
    public string UniqueKey => $"{UserIndex}:{CharacterIndex}:{Name}";
    public string Format() => $"{Name}({Kind}, {Job}, {Power:N0})";
}

internal readonly record struct PartySlot(int RaidIndex, int PartyIndex);
