namespace Emerald.Core
{
    public static class Extentions
    {
        public static string ToFullVersion(this ProjBobcat.Class.Model.Optifine.OptifineDownloadVersionModel x)
            => $"{x.McVersion}-Optifine_{x.Type}_{x.Patch}";
    }
}
