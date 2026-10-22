// Portal transport double: production routing/navigation is exercised separately
// from the native/GadgetCore portal snapshot bridge.
namespace ControllerSupport
{
    internal static class PortalMenuContext
    {
        internal const int FirstSlot=100, TargetCount=30;
        internal static bool Open, Allowed=true, NativeAllowed=true;
        internal static UnityEngine.Collider[] Targets=new UnityEngine.Collider[TargetCount];
        internal static int Prepared=-1, PrepareCalls;
        internal static bool IsOpen(GameScript game) { return game != null && Open; }
        internal static void CollectTargets(GameScript game,UnityEngine.Collider[] slots)
        { if(IsOpen(game)) for(int i=0;i<TargetCount;i++) slots[FirstSlot+i]=Targets[i]; }
        internal static bool CanActivate(GameScript game,int slot,out string reason)
        { reason="Portal unavailable"; return IsOpen(game)&&Allowed&&slot>=FirstSlot&&slot<FirstSlot+TargetCount&&Targets[slot-FirstSlot]!=null; }
        internal static string ActionLabel(int slot) { return slot==126?"Travel":"Select planet"; }
        internal static void PrepareClick(GameScript game,int slot,UnityEngine.Collider collider) { Prepared=slot; PrepareCalls++; }
        internal static bool NativeClickAllowed(GameScript game) { return NativeAllowed; }
        internal static void Reset() { Open=false; Allowed=NativeAllowed=true; Prepared=-1; PrepareCalls=0; Targets=new UnityEngine.Collider[TargetCount]; }
    }
}
