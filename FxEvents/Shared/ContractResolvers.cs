using Newtonsoft.Json.Serialization;
using System;

namespace FxEvents.Shared
{
    public class ContractResolver : DefaultContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            var contract = base.CreateContract(objectType);

            if (objectType.IsAbstract || objectType.IsInterface)
            {
                var substitute = JsonHelper.Substitutes.TryGetValue(objectType, out var result) ? result : null;

                if (substitute != null)
                {
                    contract.Converter = new TypeConverter(substitute);
                }
            }

            return contract;
        }
    }
}