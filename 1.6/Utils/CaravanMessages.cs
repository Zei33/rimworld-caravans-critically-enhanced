using Verse;

namespace CaravansCriticallyEnhanced.Utils
{
    /// <summary>
    /// The one place this mod posts a message to the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Messages.Message</c> defaults <c>historical</c> to true, which files the message in the
    /// History tab through <c>Find.Archive.Add</c>. The archive keeps 200 unpinned entries and culls
    /// the oldest first, so a mod that posts on a timer pushes real events out of the player's
    /// history. Nearly everything this mod says is that kind of message: a caravan changing exit, an
    /// item dropped from a list, a lead released. So every message goes through here, with
    /// <c>historical</c> fixed to false, and <c>Tests/CaravanMessagesTests.cs</c> fails if any other
    /// code in the mod calls <c>Messages.Message</c>.
    /// </para>
    /// </remarks>
    public static class CaravanMessages
    {
        /// <summary>
        /// Posts a message that is not filed in the History tab.
        /// </summary>
        /// <param name="text">The translated text.</param>
        /// <param name="targets">What clicking the message jumps to.</param>
        /// <param name="type">The message type, which sets its colour and sound.</param>
        public static void Post(string text, LookTargets targets, MessageTypeDef type)
        {
            Messages.Message(text, targets, type, historical: false);
        }

        /// <summary>
        /// Posts a message unless one with the same tag went out too recently.
        /// </summary>
        /// <param name="tag">Identifies the repeat, for example a key plus a lord's load id.</param>
        /// <param name="minSeconds">How long, in real seconds, before the same tag may post again.</param>
        /// <param name="text">The translated text.</param>
        /// <param name="targets">What clicking the message jumps to.</param>
        /// <param name="type">The message type.</param>
        /// <remarks>
        /// <c>MessagesRepeatAvoider</c> counts real time, so a paused game does not hold a repeat back
        /// and a fast-forwarded one does not let more through.
        /// </remarks>
        public static void PostThrottled(string tag, float minSeconds, string text, LookTargets targets, MessageTypeDef type)
        {
            if (MessagesRepeatAvoider.MessageShowAllowed(tag, minSeconds))
            {
                Post(text, targets, type);
            }
        }
    }
}
