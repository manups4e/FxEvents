using Newtonsoft.Json.Serialization;
using System;

namespace FxEvents.Shared
{
    public class ContractResolver : DefaultContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            JsonContract contract = base.CreateContract(objectType);

            if (objectType.IsAbstract || objectType.IsInterface)
            {
                Type substitute = JsonHelper.Substitutes.TryGetValue(objectType, out Type result) ? result : null;

                if (substitute != null)
                {
                    contract.Converter = new TypeConverter(substitute);
                }
            }

            return contract;
        }
    }
}