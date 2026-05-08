module github.com/RedpointGames/uet/UET/Redpoint.Uefs.Daemon.Integration.Kubernetes/go-forward

go 1.26

require (
	github.com/gogo/protobuf v1.3.2
	google.golang.org/grpc v1.81.0
	google.golang.org/protobuf v1.36.11
)

require (
	golang.org/x/net v0.53.0 // indirect
	golang.org/x/sys v0.43.0 // indirect
	golang.org/x/text v0.36.0 // indirect
	google.golang.org/genproto/googleapis/rpc v0.0.0-20260414002931-afd174a4e478 // indirect
)

replace src.redpoint.games/redpointgames/uefs/lib/go-forward/proto => ./proto
