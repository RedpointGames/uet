FROM gcr.io/google.com/cloudsdktool/cloud-sdk:latest

EXPOSE 9000
ENV CLOUDSDK_CORE_PROJECT local-dev

ENTRYPOINT [ "gcloud", "beta", "emulators", "pubsub", "start", "--host-port=0.0.0.0:9000" ]