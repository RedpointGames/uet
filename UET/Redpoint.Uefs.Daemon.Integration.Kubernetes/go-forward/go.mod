module src.redpoint.games/redpointgames/uefs/lib/go-forward

go 1.24.0

require (
	github.com/gogo/protobuf v1.3.2
	github.com/golang/protobuf v1.5.4
	google.golang.org/grpc v1.80.0
	google.golang.org/protobuf v1.36.11
)

require (
	golang.org/x/net v0.49.0 // indirect
	golang.org/x/sys v0.40.0 // indirect
	golang.org/x/text v0.33.0 // indirect
	google.golang.org/genproto v0.0.0-20230110181048-76db0878b65f // indirect
)

replace src.redpoint.games/redpointgames/uefs/lib/go-forward/proto => ./proto
