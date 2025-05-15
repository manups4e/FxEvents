using CitizenFX.Core;
using FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPackResolvers;
using FxEvents.Shared.Serialization.Implementations.MsgPackResolvers;
using FxEvents.Shared.Snowflakes;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations
{
    internal static class TypeSerializer
    {
        private static SerializationContext _context = new(PackerCompatibilityOptions.Classic) { SerializationMethod = SerializationMethod.Map, GeneratorOption = SerializationMethodGeneratorOption.Fast };
        private static readonly Type[] WriteTypes = new[] {
            typeof(string), typeof(DateTime), typeof(double),
            typeof(decimal), typeof(Guid),
        };
        public static bool IsSimpleType(this Type type)
        {
            return type.IsPrimitive || WriteTypes.Contains(type);
        }


        public static void Serialize(object obj, Shared.Serialization.SerializationContext context)
        {
            if (obj == null)
            {
                context.Writer.Write(new byte[] { 0xC0 });
                return;
            }

            using var packer = Packer.Create(context.Writer.BaseStream, PackerCompatibilityOptions.None);
            Serialize(obj, packer);
        }

        private static void Serialize(object obj, Packer packer)
        {
            if (obj == null)
            {
                packer.PackNull();

                return;
            }

            var type = obj.GetType();
            new Logger.Log().Warning("Item: " + obj + ", type: " + type.Name);


            if (type.IsSimpleType())
            {
                packer.Pack(obj);
            }
            else if (type.IsEnum)
            {
                var eTypeInfo = type.GetTypeInfo();
                var eValueType = eTypeInfo.DeclaredFields.First(x => x.Name == "value__").FieldType;
                var eAsNumber = Convert.ChangeType(obj, eValueType);
                packer.Pack(eAsNumber);
            }
            else if (obj is byte[] bytes)
            {
                packer.Pack(bytes);
            }
            else if (obj is IDictionary)
            {
                var dict = (IDictionary)obj;

                packer.PackMapHeader(dict.Count);

                foreach (var key in dict.Keys)
                {
                    Serialize(key, packer);
                    Serialize(dict[key], packer);
                }
            }
            else if (obj is IDictionary<string, object>) // special case for ExpandoObject
            {
                var dict = (IDictionary<string, object>)obj;

                packer.PackMapHeader(dict.Count);

                foreach (var kvp in dict)
                {
                    Serialize(kvp.Key, packer);
                    Serialize(kvp.Value, packer);
                }
            }
            else if (obj is IList)
            {
                var list = (IList)obj;

                packer.PackArrayHeader(list.Count);

                foreach (var item in list)
                {
                    Serialize(item, packer);
                }
            }
            else if (obj is IEnumerable enu)
            {
                var list = new List<object>();

                foreach (var item in enu)
                {
                    list.Add(item);
                }

                packer.PackArrayHeader(list.Count);

                list.ForEach(a => Serialize(a, packer));
            }
            else if (obj is IPackable packable)
            {
                packable.PackToMessage(packer, null);
            }
            // we don't support it yet
            //else if (obj is Delegate deleg)
            //{
            //    var serializer = new DelegateSerializer();
            //    serializer.PackTo(packer, deleg);
            //}
            else if (obj is Vector2 vec2)
            {
                var serializer = new Vector2Resolver(_context);
                serializer.PackTo(packer, vec2);
            }
            else if (obj is Vector3 vec3)
            {
                var serializer = new Vector3Resolver(_context);
                serializer.PackTo(packer, vec3);
            }
            else if (obj is Vector4 vec4)
            {
                var serializer = new Vector4Resolver(_context);
                serializer.PackTo(packer, vec4);
            }
            else if (obj is Quaternion quat)
            {
                var serializer = new QuaternionResolver(_context);
                serializer.PackTo(packer, quat);
            }
            else if (obj is Entity ent)
            {
                var serializer = new EntityResolver(_context);
                serializer.PackTo(packer, ent);
            }
            else if (obj is Ped ped)
            {
                var serializer = new PedResolver(_context);
                serializer.PackTo(packer, ped);
            }
            else if (obj is Vehicle veh)
            {
                var serializer = new VehicleResolver(_context);
                serializer.PackTo(packer, veh);
            }
            else if (obj is Prop prop)
            {
                var serializer = new PropResolver(_context);
                serializer.PackTo(packer, prop);
            }
            else if (obj is Matrix mat)
            {
                var serializer = new MatrixResolver(_context);
                serializer.PackTo(packer, mat);
            }
            else if (obj is Matrix3x3 mat3x3)
            {
                var serializer = new Matrix3x3Resolver(_context);
                serializer.PackTo(packer, mat3x3);
            }
            else if (obj is Snowflake snow)
            {
                var serializer = new SnowflakeResolver(_context);
                serializer.PackTo(packer, snow);
            }
            else if (obj is Player player)
            {
                var serializer = new PlayerResolver(_context);
                serializer.PackTo(packer, player);
            }
            else
            {
                var properties = type.GetProperties();
                var dict = new Dictionary<string, object>();

                foreach (var property in properties)
                {
                    dict[property.Name] = property.GetValue(obj, null);
                }

                Serialize(dict, packer);
            }
        }
    }
}
