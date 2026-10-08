
// punch attack script
public class PunchAttack : MeleeBossAttack
{
    protected override void Reset() { ConfigureDefaultPunch(); }


    public void ConfigureDefaultPunch()
    {
        attackRange = 1.1f;
        verticalReach = 0.8f;
        windupSeconds = 0.12f;
        recoverySeconds = 0.25f;
        damage = 10;
    }
}
