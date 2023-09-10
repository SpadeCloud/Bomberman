using Bomberman.Network.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Bomberman.Network
{
    public abstract class NetworkMessage
    {
        public abstract MessageType MessageType { get; }

        public NetworkMessage()
        {       }

        public NetworkMessage(byte[] msg)
        {
            using (var br = new BinaryReader(new MemoryStream(msg)))
            {
                //skip messageTYpe
                br.ReadInt32();

                Type type = this.GetType();
                PropertyInfo[] proporties = type.GetProperties();

                foreach (PropertyInfo property in proporties)
                {
                    Type propertyType = property.PropertyType;
                    //skip the messageType(int) property
                    if (propertyType == typeof(MessageType))
                        continue;

                    Read(propertyType, property, br);

                    //test
                    //Console.WriteLine($"{property.Name} is {property.GetValue(this)}");

                }
                // Step 1. Reflect the class you're inside of
                // Step 2. Find all the properties (ignoring MessageType)
                // Step 3. Read the value from "br"
                // Step 4. Set the appropriate property value
            }
        }
        private void Read(Type type, PropertyInfo property, BinaryReader br)
        {
            if (type == typeof(int))
                property.SetValue(this, br.ReadInt32());
            else if (type == typeof(byte))
                property.SetValue(this, br.ReadByte());
            else if (type == typeof(short))
                property.SetValue(this, br.ReadInt16());
            else if (type == typeof(bool))
                property.SetValue(this, br.ReadBoolean());
            else if (type == typeof(DateTime))
                property.SetValue(this, new DateTime(br.ReadInt64()));
            else if (type == typeof(string))
            {
                //read the length, str.length returns a int but in WRITE(), it was casted to BYTE
                //reconstructing the redeconstructed string
                byte strLength = br.ReadByte();
                char[] chars = new char[strLength];
                for (int i = 0; i < strLength; i++)
                    chars[i] = br.ReadChar();
                string str = new string(chars);

                property.SetValue(this, str);
            }
            else if (type.IsEnum)
            {
                //can be different sizes. readTYPE changed based on enum.underlyingTYPE... 
                //cant cut the byte[] OR indicate start position
                //seperate method impossible? 
                Type enumType = property.PropertyType.GetEnumUnderlyingType();
                Read(enumType, property, br);

            }
        }
        private void Write(Type type, object value, BinaryWriter bw)
        {
            if (type == typeof(int) && type != null)
                bw.Write((int)value);
            else if (type == typeof(byte) && type != null)
                bw.Write((byte)value);
            else if (type == typeof(short) && type != null)
                bw.Write((short)value);
            else if (type == typeof(string))
            {
                string str = (string)value ?? "";
                //length casted to a byte(0-255) cause that seems reasonable to sizing
                bw.Write((byte)str.Length); // length
                foreach (var ch in str)
                    bw.Write(ch);
            }
            else if (type.IsEnum && value != null)
            {
                var enumType = type.GetEnumUnderlyingType();
                Write(enumType, value, bw);
            }
            else if (type == typeof(bool) && value != null)
                bw.Write((bool)value);
            else if (type == typeof(DateTime) && value != null)
                bw.Write(((DateTime)value).Ticks);
            else
                throw new InvalidOperationException($"Type {type.Name} is not supported.");
        }

        public virtual byte[] ToBytes()
        {
            byte[] result;
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    //takes this from the child class(since NETWORKMESSAGE doesnt exist on-its-own)
                    bw.Write((int)MessageType);

                    //looking for values within the child class(es) to write
                    Type type = this.GetType();
                    PropertyInfo[] properties = type.GetProperties();
                    foreach (PropertyInfo property in properties)
                    {
                        Type propertyType = property.PropertyType;
                        if (propertyType == typeof(MessageType))
                            continue;

                        object propertyValue = property.GetValue(this);
                        
                        //testing
                        //Console.WriteLine($"{propertyType.Name} {property.Name} = {propertyValue}");

                        Write(propertyType, propertyValue, bw);

                    }
                    // Step 1. Reflect the class you're inside of
                    // Step 2. Find all the properties (ignoring MessageType)
                    // Step 3. Write them to "bw"

                }
                result = ms.ToArray();
            }
            return result;
        }
    }
}
