using System.Threading.Tasks;

namespace StardewAI.RuntimePrimitives
{
    public static class NativeObservationCompletion
    {
        public static async Task<T> AwaitFinalObservationAsync<T>(Task<T> native, Task finalized)
        {
            var result = await native.ConfigureAwait(false);
            await finalized.ConfigureAwait(false);
            return result;
        }
    }
}
