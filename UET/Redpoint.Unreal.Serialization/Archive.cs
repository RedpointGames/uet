namespace Redpoint.Unreal.Serialization
{
    using System.Numerics;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;

    public class Archive
    {
        private Stream _stream;

        public bool IsLoading { get; }

        public bool IsLittleEndian { get; set; } = true;

        public Archive(Stream stream, bool isLoading)
        {
            _stream = stream;
            IsLoading = isLoading;

            if (IsLoading && !_stream.CanRead)
            {
                throw new ArgumentException("UnrealArchive set to loading mode with stream that can not be read from.", nameof(isLoading));
            }
            if (!IsLoading && !_stream.CanWrite)
            {
                throw new ArgumentException("UnrealArchive set to saving mode with stream that can not be written to.", nameof(isLoading));
            }
        }

        private TTo ReinterpretCast<TFrom, TTo>(TFrom value)
        {
            unsafe
            {
                var valueRef = __makeref(value);
                var dest = default(TTo);
                var destRef = __makeref(dest);
                *(IntPtr*)&destRef = *(IntPtr*)&valueRef;
                return __refvalue(destRef, TTo);
            }
        }

        private void ByteOrderSerialize<T>(ref T value) where T : IBinaryInteger<T>, IUnsignedNumber<T>
        {
            if (IsLoading)
            {
                var span = new Span<byte>();
                Serialize(ref span, value.GetByteCount());
                if (IsLittleEndian)
                {
                    value = T.ReadLittleEndian(span, true);
                }
                else
                {
                    value = T.ReadBigEndian(span, true);
                }
            }
            else
            {
                var bytes = new byte[value.GetByteCount()];
                if (IsLittleEndian)
                {
                    value.WriteLittleEndian(bytes);
                }
                else
                {
                    value.WriteBigEndian(bytes);
                }
                var span = bytes.AsSpan();
                Serialize(ref span, span.Length);
            }
        }

        public void Serialize(ref SByte value)
        {
            var u = ReinterpretCast<SByte, Byte>(value);
            ByteOrderSerialize(ref u);
            value = ReinterpretCast<Byte, SByte>(u);
        }

        public void Serialize(ref Int16 value)
        {
            var u = ReinterpretCast<Int16, UInt16>(value);
            ByteOrderSerialize(ref u);
            value = ReinterpretCast<UInt16, Int16>(u);
        }

        public void Serialize(ref Int32 value)
        {
            var u = ReinterpretCast<Int32, UInt32>(value);
            ByteOrderSerialize(ref u);
            value = ReinterpretCast<UInt32, Int32>(u);
        }

        public void Serialize(ref Int64 value)
        {
            var u = ReinterpretCast<Int64, UInt64>(value);
            ByteOrderSerialize(ref u);
            value = ReinterpretCast<UInt64, Int64>(u);
        }

        public void Serialize(ref Byte value)
        {
            ByteOrderSerialize(ref value);
        }

        public void Serialize(ref UInt16 value)
        {
            ByteOrderSerialize(ref value);
        }

        public void Serialize(ref UInt32 value)
        {
            ByteOrderSerialize(ref value);
        }

        public void Serialize(ref UInt64 value)
        {
            ByteOrderSerialize(ref value);
        }

        public void Serialize(ref float value)
        {
            var u = ReinterpretCast<float, UInt32>(value);
            ByteOrderSerialize(ref u);
            value = ReinterpretCast<UInt32, float>(u);
        }

        public void Serialize(ref double value)
        {
            var u = ReinterpretCast<double, UInt64>(value);
            ByteOrderSerialize(ref u);
            value = ReinterpretCast<UInt64, double>(u);
        }

        public void Serialize(ref Span<byte> value, long length)
        {
            if (IsLoading)
            {
                value = new byte[length];
                _stream.ReadExactly(value);
            }
            else
            {
                _stream.Write(value);
            }
        }

        public void Serialize(ref byte[] value, long length)
        {
            if (IsLoading)
            {
                value = new byte[length];
                _stream.ReadExactly(value);
            }
            else
            {
                _stream.Write(value);
            }
        }

        public void Serialize(ref bool value)
        {
            UInt32 intValue = value ? 1u : 0u;
            ByteOrderSerialize(ref intValue);
            value = intValue == 1;
        }

        public void Serialize(ref string value, bool encodeAsASCII = false)
        {
            if (IsLoading)
            {
                Int32 length = 0;
                Serialize(ref length);

                bool encodedAsUnicode = length < 0;
                if (encodedAsUnicode)
                {
                    length = -length;
                }

                if (length > 0)
                {
                    if (encodedAsUnicode)
                    {
                        Span<byte> span = Span<byte>.Empty;
                        Serialize(ref span, length * sizeof(short));
                        if (IsLittleEndian != BitConverter.IsLittleEndian)
                        {
                            for (var i = 0; i < span.Length; i += 2)
                            {
                                var t = span[i];
                                span[i] = span[i + 1];
                                span[i + 1] = t;
                            }
                        }
                        span[length - 1] = 0;
                        value = Encoding.Unicode.GetString(span.Slice(0, span.Length - sizeof(short)));
                    }
                    else
                    {
                        Span<byte> span = Span<byte>.Empty;
                        Serialize(ref span, length);
                        span[length - 1] = 0;
                        value = Encoding.ASCII.GetString(span.Slice(0, span.Length - 1));
                    }

                    if (length == 1)
                    {
                        value = string.Empty;
                    }
                }
            }
            else
            {
                var nullTerminatedValue = value + "\0";
                Int32 length = encodeAsASCII ? nullTerminatedValue.Length : -nullTerminatedValue.Length;
                Serialize(ref length);

                if (length != 0)
                {
                    var bytes = (encodeAsASCII ? Encoding.ASCII : Encoding.Unicode).GetBytes(nullTerminatedValue);
                    if (!encodeAsASCII && IsLittleEndian != BitConverter.IsLittleEndian)
                    {
                        for (var i = 0; i < bytes.Length; i += 2)
                        {
                            var t = bytes[i];
                            bytes[i] = bytes[i + 1];
                            bytes[i + 1] = t;
                        }
                    }
                    var span = bytes.AsSpan();
                    Serialize(ref span, bytes.Length);
                }
            }
        }

        public void Serialize<T>(ref T value) where T : notnull, ISerializable<T>, new()
        {
            T.Serialize(this, ref value);
        }

        private static Dictionary<TopLevelAssetPath, Type>? _topLevelClassCache = null;
        private static object _topLevelClassLock = new();
        private static Type GetTypeForTopLevelAssetPath(TopLevelAssetPath assetPath)
        {
            if (_topLevelClassCache == null)
            {
                lock (_topLevelClassLock)
                {
                    if (_topLevelClassCache == null)
                    {
                        _topLevelClassCache = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(x => x.GetTypes())
                            .Where(x => x.GetCustomAttribute<TopLevelAssetPathAttribute>() != null)
                            .ToDictionary(k =>
                            {
                                var attr = k.GetCustomAttribute<TopLevelAssetPathAttribute>();
                                return new TopLevelAssetPath(attr!.PackageName, attr.AssetName);
                            }, v => v);
                    }
                }
            }

            if (!_topLevelClassCache.ContainsKey(assetPath))
            {
                throw new TopLevelAssetPathNotFoundException(assetPath);
            }
            return _topLevelClassCache[assetPath];
        }

        private void InvokeSerializeOnType<T>(Type type, ref T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var args = new object[] { this, value };
            type.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, BindingFlags.DoNotWrapExceptions, null, args, null);
            value = (T)args[1];
        }

        public void DynamicRpcSerialize(TopLevelAssetPath assetPath, ref object? value)
        {
            var type = GetTypeForTopLevelAssetPath(assetPath);
            if (IsLoading)
            {
                value = type.GetConstructor(Type.EmptyTypes)!.Invoke(null);
            }
            InvokeSerializeOnType(type, ref value);
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };

        public void DynamicJsonFromRemainderOfStream(TopLevelAssetPath assetPath, ref object? value)
        {
            var type = GetTypeForTopLevelAssetPath(assetPath);

            if (IsLoading)
            {
                var buffer = new byte[_stream.Length - _stream.Position];
                _stream.Read(buffer);
                var json = Encoding.Unicode.GetString(buffer);
                value = JsonSerializer.Deserialize(json, type, _jsonOptions);
            }
            else
            {
                var json = JsonSerializer.Serialize(value, type, _jsonOptions);
                var buffer = Encoding.Unicode.GetBytes(json);
                _stream.Write(buffer);
            }
        }

        public void DynamicJsonSerialize(TopLevelAssetPath assetPath, ref object? value)
        {
            var type = GetTypeForTopLevelAssetPath(assetPath);

            if (IsLoading)
            {
                string json = string.Empty;
                Serialize(ref json);
                value = JsonSerializer.Deserialize(json, type, _jsonOptions);
            }
            else
            {
                string json = JsonSerializer.Serialize(value, type, _jsonOptions);
                Serialize(ref json);
            }
        }

        public void RuntimeSerialize<T>(ref T value) where T : new()
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            switch (value)
            {
                case Int16 v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case Int32 v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case Int64 v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case UInt16 v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case UInt32 v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case UInt64 v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case float v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case double v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case bool v:
                    Serialize(ref v);
                    value = (T)(object)v;
                    return;
                case Guid v:
                    ArchiveGuid.Serialize(this, ref v);
                    value = (T)(object)v;
                    return;
                default:
                    // Now we need to compare against T
                    // because value might be a null value.
                    if (typeof(string) == typeof(T))
                    {
                        string v = (string)(object)value!;
                        Serialize(ref v);
                        value = (T)(object)v;
                        return;
                    }
                    else if (typeof(T).IsAssignableTo(typeof(ISerializable<T>)))
                    {
                        InvokeSerializeOnType(typeof(T), ref value);
                    }
                    else
                    {
                        throw new NotSupportedException($"Type {typeof(T).FullName} can not be serialized for Unreal RPCs.");
                    }
                    break;
            }
        }
    }
}