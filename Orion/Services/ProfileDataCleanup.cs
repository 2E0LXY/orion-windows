using System;
using System.Threading.Tasks;

namespace Orion
{
    /// <summary>
    /// Removes the data dirs of deleted profiles. A deleted profile keeps its
    /// registry row forever (ids are never reused), and its dirs can only be
    /// removed by a process that is not its own: the owning process's handles
    /// release only at termination, and its engine child processes exit after
    /// it. So removal runs from a surviving process — the deleter sweeps
    /// promptly after a deletion, and the catch-all net re-sweeps at every
    /// launch and on every forwarded single-instance "open" (a self-deleted
    /// profile's successor spawn reaches an already-running process as an
    /// open, not a launch).
    /// </summary>
    internal static class ProfileDataCleanup
    {
        /// <summary>Catch-all net: sweep every deleted profile's leftover dirs.</summary>
        internal static void Run()
        {
            _ = Task.Run(() =>
            {
                foreach (long id in OrionBackend.GetDeletedProfileIds())
                    Sweep(id);
            });
        }

        /// <summary>
        /// Prompt path after deleting a profile: sweep it — immediately if it
        /// is stopped, once it exits if it is running.
        /// </summary>
        internal static void RunFor(long profileId)
        {
            _ = Task.Run(() => Sweep(profileId));
        }

        /// <summary>
        /// Waits for the profile's single-instance claim to clear, then removes
        /// its dirs. The claim is released early in the owner's shutdown, before
        /// its file handles — DeleteProfileData's bounded retry bridges that
        /// gap. A wait timeout (owner hung) or an interrupted delete just
        /// leaves the dirs to a later sweep; an already-clean profile is a
        /// cheap no-op.
        /// </summary>
        private static void Sweep(long profileId)
        {
            if (SingleInstance.WaitForExit(profileId, TimeSpan.FromSeconds(30)))
                OrionBackend.DeleteProfileData(profileId);
        }
    }
}
