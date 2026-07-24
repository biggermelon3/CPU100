// CPU load stage shared across modules (contract §5.1).
// Value ranges (see CPUManager.StageForValue):
//   <=30 Normal, <=50 LightLoad, <=70 MediumLoad, <=85 HeavyLoad, <100 Critical, else Crashed.
public enum CPUStage
{
    Normal,
    LightLoad,
    MediumLoad,
    HeavyLoad,
    Critical,
    Crashed
}
