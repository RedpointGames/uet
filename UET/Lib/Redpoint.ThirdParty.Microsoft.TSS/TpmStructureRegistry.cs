namespace TSS.Net
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tpm2Lib;

    internal static class TpmStructureRegistry
    {
        private static HashSet<Type> _types = new();

        private static void R<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : TpmStructureBase
        {
            _types.Add(typeof(T));
        }

        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2068",
            Justification = "This method will throw if an unregistered type is used.")]
        public static Type Get(Type type)
        {
            if (_types.Contains(type))
            {
                return type;
            }

            throw new InvalidOperationException($"{type.FullName} is not registered with type registry.");
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2067",
            Justification = "This method will throw if an unregistered type is used.")]
        public static object Create(Type type)
        {
            if (_types.Contains(type))
            {
                return Activator.CreateInstance(type);
            }

            throw new InvalidOperationException($"{type.FullName} is not registered with type registry.");
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050",
            Justification = "This method will throw if an unregistered type is used.")]
        public static Array CreateArray(Type type, int length)
        {
            if (type == typeof(byte))
            {
                return Array.CreateInstance(type, length);
            }

            if (_types.Contains(type))
            {
                return Array.CreateInstance(type, length);
            }

            throw new InvalidOperationException($"{type.FullName} is not registered with type registry.");
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "")]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2111", Justification = "")]
        static TpmStructureRegistry()
        {
            R<EmptyResponse>();
            R<TpmHash>();
            R<AuthValue>();
            R<RsaParms>();
            R<TpmHandle>();
            R<PcrSelect>();
            R<PcrSelection>();
            R<PcrValue>();
            R<PcrValueCollection>();
            R<Attest>();
            R<SymDef>();
            R<SymDefObject>();
            R<SensitiveCreate>();
            R<EccPoint>();
            R<NvPublic>();
            R<TpmPublic>();
            R<TssObject>();
            R<TpmHandle>();
            R<NullUnion>();
            R<Empty>();
            R<AlgorithmDescription>();
            R<TpmHash>();
            R<Tpm2bDigest>();
            R<Tpm2bData>();
            R<Tpm2bNonce>();
            R<Tpm2bAuth>();
            R<Tpm2bOperand>();
            R<Tpm2bEvent>();
            R<Tpm2bMaxBuffer>();
            R<Tpm2bMaxNvBuffer>();
            R<Tpm2bTimeout>();
            R<Tpm2bIv>();
            R<Tpm2bName>();
            R<PcrSelect>();
            R<PcrSelection>();
            R<TkCreation>();
            R<TkVerified>();
            R<TkAuth>();
            R<TkHashcheck>();
            R<AlgProperty>();
            R<TaggedProperty>();
            R<TaggedPcrSelect>();
            R<TaggedPolicy>();
            R<ActData>();
            R<CcArray>();
            R<CcaArray>();
            R<AlgArray>();
            R<HandleArray>();
            R<DigestArray>();
            R<DigestValuesArray>();
            R<PcrSelectionArray>();
            R<AlgPropertyArray>();
            R<TaggedTpmPropertyArray>();
            R<TaggedPcrPropertyArray>();
            R<EccCurveArray>();
            R<TaggedPolicyArray>();
            R<ActDataArray>();
            R<CapabilityData>();
            R<ClockInfo>();
            R<TimeInfo>();
            R<TimeAttestInfo>();
            R<CertifyInfo>();
            R<QuoteInfo>();
            R<CommandAuditInfo>();
            R<SessionAuditInfo>();
            R<CreationInfo>();
            R<NvCertifyInfo>();
            R<NvDigestCertifyInfo>();
            R<Attest>();
            R<Tpm2bAttest>();
            R<AuthCommand>();
            R<AuthResponse>();
            R<TdesSymDetails>();
            R<AesSymDetails>();
            R<Sm4SymDetails>();
            R<CamelliaSymDetails>();
            R<AnySymDetails>();
            R<XorSymDetails>();
            R<NullSymDetails>();
            R<SymDef>();
            R<SymDefObject>();
            R<Tpm2bSymKey>();
            R<SymcipherParms>();
            R<Tpm2bLabel>();
            R<TpmDerive>();
            R<Tpm2bDerive>();
            R<Tpm2bSensitiveData>();
            R<SensitiveCreate>();
            R<Tpm2bSensitiveCreate>();
            R<SchemeHash>();
            R<SchemeEcdaa>();
            R<SchemeHmac>();
            R<SchemeXor>();
            R<NullSchemeKeyedhash>();
            R<KeyedhashScheme>();
            R<SigSchemeRsassa>();
            R<SigSchemeRsapss>();
            R<SigSchemeEcdsa>();
            R<SigSchemeSm2>();
            R<SigSchemeEcschnorr>();
            R<SigSchemeEcdaa>();
            R<NullSigScheme>();
            R<SigScheme>();
            R<EncSchemeOaep>();
            R<EncSchemeRsaes>();
            R<KeySchemeEcdh>();
            R<KeySchemeEcmqv>();
            R<KdfSchemeMgf1>();
            R<KdfSchemeKdf1Sp80056a>();
            R<KdfSchemeKdf2>();
            R<KdfSchemeKdf1Sp800108>();
            R<NullKdfScheme>();
            R<KdfScheme>();
            R<NullAsymScheme>();
            R<AsymScheme>();
            R<RsaScheme>();
            R<RsaDecrypt>();
            R<Tpm2bPublicKeyRsa>();
            R<Tpm2bPrivateKeyRsa>();
            R<Tpm2bEccParameter>();
            R<EccPoint>();
            R<Tpm2bEccPoint>();
            R<EccScheme>();
            R<AlgorithmDetailEcc>();
            R<SignatureRsa>();
            R<SignatureRsassa>();
            R<SignatureRsapss>();
            R<SignatureEcc>();
            R<SignatureEcdsa>();
            R<SignatureEcdaa>();
            R<SignatureSm2>();
            R<SignatureEcschnorr>();
            R<NullSignature>();
            R<Signature>();
            R<Tpm2bEncryptedSecret>();
            R<KeyedhashParms>();
            R<AsymParms>();
            R<RsaParms>();
            R<EccParms>();
            R<PublicParms>();
            R<TpmPublic>();
            R<Tpm2bPublic>();
            R<Tpm2bTemplate>();
            R<Tpm2bPrivateVendorSpecific>();
            R<Sensitive>();
            R<Tpm2bSensitive>();
            R<_Private>();
            R<TpmPrivate>();
            R<IdObject>();
            R<Tpm2bIdObject>();
            R<NvPinCounterParameters>();
            R<NvPublic>();
            R<Tpm2bNvPublic>();
            R<Tpm2bContextSensitive>();
            R<ContextData>();
            R<Tpm2bContextData>();
            R<Context>();
            R<CreationData>();
            R<Tpm2bCreationData>();
            R<AcOutput>();
            R<AcCapabilitiesArray>();
            R<Tpm2StartupRequest>();
            R<Tpm2ShutdownRequest>();
            R<Tpm2SelfTestRequest>();
            R<Tpm2IncrementalSelfTestRequest>();
            R<Tpm2IncrementalSelfTestResponse>();
            R<Tpm2GetTestResultRequest>();
            R<Tpm2GetTestResultResponse>();
            R<Tpm2StartAuthSessionRequest>();
            R<Tpm2StartAuthSessionResponse>();
            R<Tpm2PolicyRestartRequest>();
            R<Tpm2CreateRequest>();
            R<Tpm2CreateResponse>();
            R<Tpm2LoadRequest>();
            R<Tpm2LoadResponse>();
            R<Tpm2LoadExternalRequest>();
            R<Tpm2LoadExternalResponse>();
            R<Tpm2ReadPublicRequest>();
            R<Tpm2ReadPublicResponse>();
            R<Tpm2ActivateCredentialRequest>();
            R<Tpm2ActivateCredentialResponse>();
            R<Tpm2MakeCredentialRequest>();
            R<Tpm2MakeCredentialResponse>();
            R<Tpm2UnsealRequest>();
            R<Tpm2UnsealResponse>();
            R<Tpm2ObjectChangeAuthRequest>();
            R<Tpm2ObjectChangeAuthResponse>();
            R<Tpm2CreateLoadedRequest>();
            R<Tpm2CreateLoadedResponse>();
            R<Tpm2DuplicateRequest>();
            R<Tpm2DuplicateResponse>();
            R<Tpm2RewrapRequest>();
            R<Tpm2RewrapResponse>();
            R<Tpm2ImportRequest>();
            R<Tpm2ImportResponse>();
            R<Tpm2RsaEncryptRequest>();
            R<Tpm2RsaEncryptResponse>();
            R<Tpm2RsaDecryptRequest>();
            R<Tpm2RsaDecryptResponse>();
            R<Tpm2EcdhKeyGenRequest>();
            R<Tpm2EcdhKeyGenResponse>();
            R<Tpm2EcdhZGenRequest>();
            R<Tpm2EcdhZGenResponse>();
            R<Tpm2ZGen2PhaseRequest>();
            R<Tpm2ZGen2PhaseResponse>();
            R<Tpm2EccEncryptRequest>();
            R<Tpm2EccEncryptResponse>();
            R<Tpm2EccDecryptRequest>();
            R<Tpm2EccDecryptResponse>();
            R<Tpm2EncryptDecryptRequest>();
            R<Tpm2EncryptDecryptResponse>();
            R<Tpm2EncryptDecrypt2Request>();
            R<Tpm2EncryptDecrypt2Response>();
            R<Tpm2HashRequest>();
            R<Tpm2HashResponse>();
            R<Tpm2HmacRequest>();
            R<Tpm2HmacResponse>();
            R<Tpm2MacRequest>();
            R<Tpm2MacResponse>();
            R<Tpm2GetRandomRequest>();
            R<Tpm2GetRandomResponse>();
            R<Tpm2StirRandomRequest>();
            R<Tpm2HmacStartRequest>();
            R<Tpm2HmacStartResponse>();
            R<Tpm2MacStartRequest>();
            R<Tpm2MacStartResponse>();
            R<Tpm2HashSequenceStartRequest>();
            R<Tpm2HashSequenceStartResponse>();
            R<Tpm2SequenceUpdateRequest>();
            R<Tpm2SequenceCompleteRequest>();
            R<Tpm2SequenceCompleteResponse>();
            R<Tpm2EventSequenceCompleteRequest>();
            R<Tpm2EventSequenceCompleteResponse>();
            R<Tpm2CertifyRequest>();
            R<Tpm2CertifyResponse>();
            R<Tpm2CertifyCreationRequest>();
            R<Tpm2CertifyCreationResponse>();
            R<Tpm2QuoteRequest>();
            R<Tpm2QuoteResponse>();
            R<Tpm2GetSessionAuditDigestRequest>();
            R<Tpm2GetSessionAuditDigestResponse>();
            R<Tpm2GetCommandAuditDigestRequest>();
            R<Tpm2GetCommandAuditDigestResponse>();
            R<Tpm2GetTimeRequest>();
            R<Tpm2GetTimeResponse>();
            R<Tpm2CertifyX509Request>();
            R<Tpm2CertifyX509Response>();
            R<Tpm2CommitRequest>();
            R<Tpm2CommitResponse>();
            R<Tpm2EcEphemeralRequest>();
            R<Tpm2EcEphemeralResponse>();
            R<Tpm2VerifySignatureRequest>();
            R<Tpm2VerifySignatureResponse>();
            R<Tpm2SignRequest>();
            R<Tpm2SignResponse>();
            R<Tpm2SetCommandCodeAuditStatusRequest>();
            R<Tpm2PcrExtendRequest>();
            R<Tpm2PcrEventRequest>();
            R<Tpm2PcrEventResponse>();
            R<Tpm2PcrReadRequest>();
            R<Tpm2PcrReadResponse>();
            R<Tpm2PcrAllocateRequest>();
            R<Tpm2PcrAllocateResponse>();
            R<Tpm2PcrSetAuthPolicyRequest>();
            R<Tpm2PcrSetAuthValueRequest>();
            R<Tpm2PcrResetRequest>();
            R<Tpm2PolicySignedRequest>();
            R<Tpm2PolicySignedResponse>();
            R<Tpm2PolicySecretRequest>();
            R<Tpm2PolicySecretResponse>();
            R<Tpm2PolicyTicketRequest>();
            R<Tpm2PolicyORRequest>();
            R<Tpm2PolicyPCRRequest>();
            R<Tpm2PolicyLocalityRequest>();
            R<Tpm2PolicyNVRequest>();
            R<Tpm2PolicyCounterTimerRequest>();
            R<Tpm2PolicyCommandCodeRequest>();
            R<Tpm2PolicyPhysicalPresenceRequest>();
            R<Tpm2PolicyCpHashRequest>();
            R<Tpm2PolicyNameHashRequest>();
            R<Tpm2PolicyDuplicationSelectRequest>();
            R<Tpm2PolicyAuthorizeRequest>();
            R<Tpm2PolicyAuthValueRequest>();
            R<Tpm2PolicyPasswordRequest>();
            R<Tpm2PolicyGetDigestRequest>();
            R<Tpm2PolicyGetDigestResponse>();
            R<Tpm2PolicyNvWrittenRequest>();
            R<Tpm2PolicyTemplateRequest>();
            R<Tpm2PolicyAuthorizeNVRequest>();
            R<Tpm2CreatePrimaryRequest>();
            R<Tpm2CreatePrimaryResponse>();
            R<Tpm2HierarchyControlRequest>();
            R<Tpm2SetPrimaryPolicyRequest>();
            R<Tpm2ChangePPSRequest>();
            R<Tpm2ChangeEPSRequest>();
            R<Tpm2ClearRequest>();
            R<Tpm2ClearControlRequest>();
            R<Tpm2HierarchyChangeAuthRequest>();
            R<Tpm2DictionaryAttackLockResetRequest>();
            R<Tpm2DictionaryAttackParametersRequest>();
            R<Tpm2PpCommandsRequest>();
            R<Tpm2SetAlgorithmSetRequest>();
            R<Tpm2FieldUpgradeStartRequest>();
            R<Tpm2FieldUpgradeDataRequest>();
            R<Tpm2FieldUpgradeDataResponse>();
            R<Tpm2FirmwareReadRequest>();
            R<Tpm2FirmwareReadResponse>();
            R<Tpm2ContextSaveRequest>();
            R<Tpm2ContextSaveResponse>();
            R<Tpm2ContextLoadRequest>();
            R<Tpm2ContextLoadResponse>();
            R<Tpm2FlushContextRequest>();
            R<Tpm2EvictControlRequest>();
            R<Tpm2ReadClockRequest>();
            R<Tpm2ReadClockResponse>();
            R<Tpm2ClockSetRequest>();
            R<Tpm2ClockRateAdjustRequest>();
            R<Tpm2GetCapabilityRequest>();
            R<Tpm2GetCapabilityResponse>();
            R<Tpm2TestParmsRequest>();
            R<Tpm2NvDefineSpaceRequest>();
            R<Tpm2NvUndefineSpaceRequest>();
            R<Tpm2NvUndefineSpaceSpecialRequest>();
            R<Tpm2NvReadPublicRequest>();
            R<Tpm2NvReadPublicResponse>();
            R<Tpm2NvWriteRequest>();
            R<Tpm2NvIncrementRequest>();
            R<Tpm2NvExtendRequest>();
            R<Tpm2NvSetBitsRequest>();
            R<Tpm2NvWriteLockRequest>();
            R<Tpm2NvGlobalWriteLockRequest>();
            R<Tpm2NvReadRequest>();
            R<Tpm2NvReadResponse>();
            R<Tpm2NvReadLockRequest>();
            R<Tpm2NvChangeAuthRequest>();
            R<Tpm2NvCertifyRequest>();
            R<Tpm2NvCertifyResponse>();
            R<Tpm2AcGetCapabilityRequest>();
            R<Tpm2AcGetCapabilityResponse>();
            R<Tpm2AcSendRequest>();
            R<Tpm2AcSendResponse>();
            R<Tpm2PolicyAcSendSelectRequest>();
            R<Tpm2ActSetTimeoutRequest>();
            R<Tpm2VendorTcgTestRequest>();
            R<Tpm2VendorTcgTestResponse>();
            R<SchemeRsassa>();
            R<SchemeRsapss>();
            R<SchemeEcdsa>();
            R<SchemeSm2>();
            R<SchemeEcschnorr>();
            R<SchemeOaep>();
            R<SchemeRsaes>();
            R<SchemeEcdh>();
            R<SchemeEcmqv>();
            R<SchemeMgf1>();
            R<SchemeKdf1Sp80056a>();
            R<SchemeKdf2>();
            R<SchemeKdf1Sp800108>();
            R<TssObject>();
            R<PcrValue>();
            R<SessionIn>();
            R<SessionOut>();
            R<CommandHeader>();
            R<TssKey>();
            R<Tpm2bDigestSymcipher>();
            R<Tpm2bDigestKeyedhash>();
        }
    }
}
