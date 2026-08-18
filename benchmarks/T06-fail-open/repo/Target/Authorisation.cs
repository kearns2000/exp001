namespace Target;
public static class Authorisation
{
    public static bool Check(Func<bool> evaluator)
    {
        try { return evaluator(); }
        catch { return true; }
    }
}
