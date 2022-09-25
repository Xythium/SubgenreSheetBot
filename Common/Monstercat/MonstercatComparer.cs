using System.Collections.Generic;

namespace Common.Monstercat;

public class MonstercatComparer : IEqualityComparer<MonstercatReleaseSummary>
{
    public bool Equals(MonstercatReleaseSummary x, MonstercatReleaseSummary y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (ReferenceEquals(x, null))
            return false;
        if (ReferenceEquals(y, null))
            return false;
        if (x.GetType() != y.GetType())
            return false;

        return x.Id.Equals(y.Id);
    }

    public int GetHashCode(MonstercatReleaseSummary obj) { return obj.Id.GetHashCode(); }
}