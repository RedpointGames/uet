FROM gcr.io/google.com/cloudsdktool/cloud-sdk:latest

EXPOSE 9001
ENV CLOUDSDK_CORE_PROJECT local-dev

# DO NOT UNDER ANY CIRCUMSTANCES ADD THE --use-firestore-in-datastore-mode FLAG
#
# This flag does not properly emulate "Firestore in Datastore mode" and *actively* breaks
# transaction integrity in the emulator, leading to transaction commits going through
# when they should fail with contention errors.
#
ENTRYPOINT [ "gcloud", "beta", "emulators", "datastore", "start", "--host-port=0.0.0.0:9001", "--no-store-on-disk", "--consistency=1.0" ]