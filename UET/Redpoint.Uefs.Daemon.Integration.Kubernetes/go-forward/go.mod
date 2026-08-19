module github.com/RedpointGames/uet/UET/Redpoint.Uefs.Daemon.Integration.Kubernetes/go-forward

go 1.26

require (
	github.com/gogo/protobuf v1.3.2
	google.golang.org/grpc v1.83.0
	google.golang.org/protobuf v1.36.11
)

require (
	golang.org/x/net v0.55.0 // indirect
	golang.org/x/sys v0.45.0 // indirect
	golang.org/x/text v0.37.0 // indirect
	google.golang.org/genproto/googleapis/rpc v0.0.0-20260526163538-3dc84a4a5aaa // indirect
)

replace src.redpoint.games/redpointgames/uefs/lib/go-forward/proto => ./proto
