namespace OledProtect.Infrastructure.Helpers
{
    public static class BuildMode
    {
        // Set by publishing with -p:Portable=true (defines PORTABLE). A portable build stores its data
        // next to the exe instead of in the user's profile.
        public const bool IsPortable =
#if PORTABLE
            true;
#else
            false;
#endif
    }
}
