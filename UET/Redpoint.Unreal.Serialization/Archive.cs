namespace Redpoint.Unreal.Serialization
{
    using System.Numerics;
    using System.Text;
    using System.Text.Json;

    public class Archive
    {
        private Stream _stream;
        private readonly ISerializerRegistry[] _serializerRegistries;

        public bool IsLoading { get; }

        public bool IsLittleEndian { get; set; } = true;

        public Archive(
            Stream stream,
            bool isLoading,
            ISerializerRegistry[] serializerRegistries)
        {
            _stream = stream;
            IsLoading = isLoading;
            _serializerRegistries = new[] { new BuiltinUnrealSerializerRegistry() }.Concat(serializerRegistries).ToArray();

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
#pragma warning disable CS8500
                *(IntPtr*)&destRef = *(IntPtr*)&valueRef;
#pragma warning restore CS8500
                return __refvalue(destRef, TTo);
            }
        }

        private async ValueTask ByteOrderSerialize<T>(Store<T> value) where T : IBinaryInteger<T>, IUnsignedNumber<T>
        {
            if (IsLoading)
            {
                var span = new Store<Memory<byte>>(new Memory<byte>());
                await Serialize(span, value.V.GetByteCount()).ConfigureAwait(false);
                if (IsLittleEndian)
                {
                    value.V = T.ReadLittleEndian(span.V.Span, true);
                }
                else
                {
                    value.V = T.ReadBigEndian(span.V.Span, true);
                }
            }
            else
            {
                var bytes = new byte[value.V.GetByteCount()];
                if (IsLittleEndian)
                {
                    value.V.WriteLittleEndian(bytes);
                }
                else
                {
                    value.V.WriteBigEndian(bytes);
                }
                var span = new Store<Memory<byte>>(bytes.AsMemory());
                await Serialize(span, span.V.Length).ConfigureAwait(false);
            }
        }

        public async ValueTask Serialize(Store<SByte> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var u = new Store<Byte>(ReinterpretCast<SByte, Byte>(value.V));
            await ByteOrderSerialize(u).ConfigureAwait(false);
            value.V = ReinterpretCast<Byte, SByte>(u.V);
        }

        public async ValueTask Serialize(Store<Int16> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var u = new Store<UInt16>(ReinterpretCast<Int16, UInt16>(value.V));
            await ByteOrderSerialize(u).ConfigureAwait(false);
            value.V = ReinterpretCast<UInt16, Int16>(u.V);
        }

        public async ValueTask Serialize(Store<Int32> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var u = new Store<UInt32>(ReinterpretCast<Int32, UInt32>(value.V));
            await ByteOrderSerialize(u).ConfigureAwait(false);
            value.V = ReinterpretCast<UInt32, Int32>(u.V);
        }

        public async ValueTask Serialize(Store<Int64> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var u = new Store<UInt64>(ReinterpretCast<Int64, UInt64>(value.V));
            await ByteOrderSerialize(u).ConfigureAwait(false);
            value.V = ReinterpretCast<UInt64, Int64>(u.V);
        }

        public async ValueTask Serialize(Store<Byte> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            await ByteOrderSerialize(value).ConfigureAwait(false);
        }

        public async ValueTask Serialize(Store<UInt16> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            await ByteOrderSerialize(value).ConfigureAwait(false);
        }

        public async ValueTask Serialize(Store<UInt32> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            await ByteOrderSerialize(value).ConfigureAwait(false);
        }

        public async ValueTask Serialize(Store<UInt64> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            await ByteOrderSerialize(value).ConfigureAwait(false);
        }

        public async ValueTask Serialize(Store<float> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var u = new Store<UInt32>(ReinterpretCast<float, UInt32>(value.V));
            await ByteOrderSerialize(u).ConfigureAwait(false);
            value.V = ReinterpretCast<UInt32, float>(u.V);
        }

        public async ValueTask Serialize(Store<double> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var u = new Store<UInt64>(ReinterpretCast<double, UInt64>(value.V));
            await ByteOrderSerialize(u).ConfigureAwait(false);
            value.V = ReinterpretCast<UInt64, double>(u.V);
        }

        public async ValueTask Serialize(Store<Memory<byte>> value, long length)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (IsLoading)
            {
                value.V = new byte[length];
                await _stream.ReadExactlyAsync(value.V).ConfigureAwait(false);
            }
            else
            {
                await _stream.WriteAsync(value.V).ConfigureAwait(false);
            }
        }

        public async ValueTask Serialize(Store<byte[]> value, long length)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (IsLoading)
            {
                value.V = new byte[length];
                await _stream.ReadExactlyAsync(value.V).ConfigureAwait(false);
            }
            else
            {
                await _stream.WriteAsync(value.V).ConfigureAwait(false);
            }
        }

        public async ValueTask Serialize(Store<bool> value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            Store<UInt32> intValue = new Store<UInt32>(value.V ? 1u : 0u);
            await ByteOrderSerialize(intValue).ConfigureAwait(false);
            value.V = intValue.V == 1;
        }

        public async ValueTask Serialize(Store<string> value, bool encodeAsASCII = false)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (IsLoading)
            {
                Store<Int32> length = new Store<Int32>(0);
                await Serialize(length).ConfigureAwait(false);

                bool encodedAsUnicode = length.V < 0;
                if (encodedAsUnicode)
                {
                    length.V = -length.V;
                }

                if (length.V > 0)
                {
                    if (encodedAsUnicode)
                    {
                        var span = new Store<Memory<byte>>(Memory<byte>.Empty);
                        await Serialize(span, length.V * sizeof(short)).ConfigureAwait(false);
                        if (IsLittleEndian != BitConverter.IsLittleEndian)
                        {
                            for (var i = 0; i < span.V.Length; i += 2)
                            {
                                var t = span.V.Span[i];
                                span.V.Span[i] = span.V.Span[i + 1];
                                span.V.Span[i + 1] = t;
                            }
                        }
                        span.V.Span[length.V - 1] = 0;
                        value.V = Encoding.Unicode.GetString(span.V.Span[..^2]);
                    }
                    else
                    {
                        var span = new Store<Memory<byte>>(Memory<byte>.Empty);
                        await Serialize(span, length.V).ConfigureAwait(false);
                        span.V.Span[length.V - 1] = 0;
                        value.V = Encoding.ASCII.GetString(span.V.Span[..^1]);
                    }

                    if (length.V == 1)
                    {
                        value.V = string.Empty;
                    }
                }
            }
            else
            {
                var nullTerminatedValue = value + "\0";
                Store<Int32> length = new Store<Int32>(encodeAsASCII ? nullTerminatedValue.Length : -nullTerminatedValue.Length);
                await Serialize(length).ConfigureAwait(false);

                if (length.V != 0)
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
                    var span = new Store<Memory<byte>>(bytes.AsMemory());
                    await Serialize(span, bytes.Length).ConfigureAwait(false);
                }
            }
        }

        public async Task Serialize<T>(Store<T> value) where T : notnull, ISerializable<T>, new()
        {
            await T.Serialize(this, value).ConfigureAwait(false);
        }

        private async Task InvokeSerializeOnType<T>(Type type, Store<T> value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            foreach (var serializerRegistry in _serializerRegistries)
            {
                if (serializerRegistry.CanHandleStoreType(type))
                {
                    await serializerRegistry.SerializeStoreType(this, value).ConfigureAwait(false);
                    return;
                }
            }

            throw new InvalidOperationException($"There is no serializer registry for the type {type.FullName}. Make sure you've generated a serializer registry with the source generator and have passed an instance of it into the Archive constructor.");
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };

        public async Task DynamicJsonFromRemainderOfStream(Store<TopLevelAssetPath> assetPath, Store<object?> value)
        {
            if (assetPath == null) throw new ArgumentNullException(nameof(assetPath));
            if (value == null) throw new ArgumentNullException(nameof(value));

            foreach (var serializerRegistry in _serializerRegistries)
            {
                if (serializerRegistry.CanHandleTopLevelAssetPath(assetPath.V))
                {
                    if (IsLoading)
                    {
                        var buffer = new byte[_stream.Length - _stream.Position];
                        await _stream.ReadAsync(buffer).ConfigureAwait(false);
                        var json = Encoding.Unicode.GetString(buffer);
                        value.V = serializerRegistry.DeserializeTopLevelAssetPath(assetPath.V, json, _jsonOptions);
                    }
                    else
                    {
                        var json = serializerRegistry.SerializeTopLevelAssetPath(assetPath.V, value.V, _jsonOptions);
                        var buffer = Encoding.Unicode.GetBytes(json);
                        await _stream.WriteAsync(buffer).ConfigureAwait(false);
                    }
                    return;
                }
            }

            throw new TopLevelAssetPathNotFoundException($"There is no serializer registry for the asset path {assetPath.V}. Make sure you've generated a serializer registry with the source generator and have passed an instance of it into the Archive constructor.");
        }

        public async Task DynamicJsonSerialize(Store<TopLevelAssetPath> assetPath, Store<object?> value)
        {
            if (assetPath == null) throw new ArgumentNullException(nameof(assetPath));
            if (value == null) throw new ArgumentNullException(nameof(value));

            foreach (var serializerRegistry in _serializerRegistries)
            {
                if (serializerRegistry.CanHandleTopLevelAssetPath(assetPath.V))
                {
                    if (IsLoading)
                    {
                        Store<string> json = new Store<string>(string.Empty);
                        await Serialize(json).ConfigureAwait(false);
                        value.V = serializerRegistry.DeserializeTopLevelAssetPath(assetPath.V, json.V, _jsonOptions);
                    }
                    else
                    {
                        Store<string> json = new Store<string>(serializerRegistry.SerializeTopLevelAssetPath(assetPath.V, value.V, _jsonOptions));
                        await Serialize(json).ConfigureAwait(false);
                    }
                    return;
                }
            }

            throw new TopLevelAssetPathNotFoundException($"There is no serializer registry for the asset path {assetPath.V}. Make sure you've generated a serializer registry with the source generator and have passed an instance of it into the Archive constructor.");
        }

        public async Task RuntimeSerialize<T>(Store<T> value) where T : new()
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (value.V == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            switch (value.V)
            {
                case Int16 v:
                    await Serialize(new CastedStore<short, T>(value)).ConfigureAwait(false);
                    return;
                case Int32 v:
                    await Serialize(new CastedStore<int, T>(value)).ConfigureAwait(false);
                    return;
                case Int64 v:
                    await Serialize(new CastedStore<long, T>(value)).ConfigureAwait(false);
                    return;
                case UInt16 v:
                    await Serialize(new CastedStore<ushort, T>(value)).ConfigureAwait(false);
                    return;
                case UInt32 v:
                    await Serialize(new CastedStore<uint, T>(value)).ConfigureAwait(false);
                    return;
                case UInt64 v:
                    await Serialize(new CastedStore<ulong, T>(value)).ConfigureAwait(false);
                    return;
                case float v:
                    await Serialize(new CastedStore<float, T>(value)).ConfigureAwait(false);
                    return;
                case double v:
                    await Serialize(new CastedStore<double, T>(value)).ConfigureAwait(false);
                    return;
                case bool v:
                    await Serialize(new CastedStore<bool, T>(value)).ConfigureAwait(false);
                    return;
                case Guid v:
                    await ArchiveGuid.Serialize(this, new CastedStore<Guid, T>(value)).ConfigureAwait(false);
                    return;
                default:
                    // Now we need to compare against T
                    // because value might be a null value.
                    if (typeof(string) == typeof(T))
                    {
                        var v = new Store<string>((string)(object)value!);
                        await Serialize(v).ConfigureAwait(false);
                        value.V = (T)(object)v;
                        return;
                    }
                    else if (typeof(T).IsAssignableTo(typeof(ISerializable<T>)))
                    {
                        await InvokeSerializeOnType(typeof(T), value).ConfigureAwait(false);
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