/*using System;
using System.Threading.Tasks;

namespace FxEvents.Shared.Snowflake
{
    internal static class Program
    {
        public static void Main()
        {
            var timestamp = Clock.GetMilliseconds();

            // Create the singleton instance generator with the specified instance id,
            //   this id represents which machine or project generated this id, used to minify collisions and can be handy for identifiers.
            SnowflakeGenerator.Create(1);

            GenerateWave();
            
            Console.WriteLine("...");
            Task.Delay(100).GetAwaiter().GetResult();
            
            GenerateWave();
            Console.WriteLine($"Took {Clock.GetMilliseconds() - timestamp - 100}ms");
        }

        private static void GenerateWave()
        {
            for (var index = 0; index < 10; index++)
            {
                // var snowflake = Snowflake.Parse( snowflake id )
                var snowflake = Snowflake.Next();

                // ... then you can deconstruct this id to extract the time, instance and sequence bits
                var fragments = snowflake.Deconstruct();

                Console.WriteLine(
                    $"#{index + 1}: {snowflake} (time = {fragments.Timestamp}, instance_id = {fragments.Instance}, sequence = {fragments.Sequence})");
            }
        }
    }
}
*/