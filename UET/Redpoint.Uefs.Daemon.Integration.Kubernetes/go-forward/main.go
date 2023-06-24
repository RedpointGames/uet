package main

import (
	"context"
	"flag"
	"fmt"
	"log"
	"net"
	"os"

	go_forward_proto "src.redpoint.games/redpointgames/uefs/lib/go-forward/proto"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type proxyingServer struct {
	go_forward_proto.UnimplementedIdentityServer
	go_forward_proto.UnimplementedNodeServer
	go_forward_proto.UnimplementedRegistrationServer

	node         go_forward_proto.NodeClient
	identity     go_forward_proto.IdentityClient
	registration go_forward_proto.RegistrationClient
}

// IdentityServer

func (s *proxyingServer) GetPluginInfo(ctx context.Context, req *go_forward_proto.GetPluginInfoRequest) (*go_forward_proto.GetPluginInfoResponse, error) {
	return s.identity.GetPluginInfo(ctx, req)
}

func (s *proxyingServer) GetPluginCapabilities(ctx context.Context, req *go_forward_proto.GetPluginCapabilitiesRequest) (*go_forward_proto.GetPluginCapabilitiesResponse, error) {
	return s.identity.GetPluginCapabilities(ctx, req)
}

func (s *proxyingServer) Probe(ctx context.Context, req *go_forward_proto.ProbeRequest) (*go_forward_proto.ProbeResponse, error) {
	return s.identity.Probe(ctx, req)
}

// NodeServer

func (s *proxyingServer) NodeStageVolume(ctx context.Context, req *go_forward_proto.NodeStageVolumeRequest) (*go_forward_proto.NodeStageVolumeResponse, error) {
	return s.node.NodeStageVolume(ctx, req)
}

func (s *proxyingServer) NodeUnstageVolume(ctx context.Context, req *go_forward_proto.NodeUnstageVolumeRequest) (*go_forward_proto.NodeUnstageVolumeResponse, error) {
	return s.node.NodeUnstageVolume(ctx, req)
}

func (s *proxyingServer) NodePublishVolume(ctx context.Context, req *go_forward_proto.NodePublishVolumeRequest) (*go_forward_proto.NodePublishVolumeResponse, error) {
	return s.node.NodePublishVolume(ctx, req)
}

func (s *proxyingServer) NodeUnpublishVolume(ctx context.Context, req *go_forward_proto.NodeUnpublishVolumeRequest) (*go_forward_proto.NodeUnpublishVolumeResponse, error) {
	return s.node.NodeUnpublishVolume(ctx, req)
}

func (s *proxyingServer) NodeGetVolumeStats(ctx context.Context, req *go_forward_proto.NodeGetVolumeStatsRequest) (*go_forward_proto.NodeGetVolumeStatsResponse, error) {
	return s.node.NodeGetVolumeStats(ctx, req)
}

func (s *proxyingServer) NodeExpandVolume(ctx context.Context, req *go_forward_proto.NodeExpandVolumeRequest) (*go_forward_proto.NodeExpandVolumeResponse, error) {
	return s.node.NodeExpandVolume(ctx, req)
}

func (s *proxyingServer) NodeGetCapabilities(ctx context.Context, req *go_forward_proto.NodeGetCapabilitiesRequest) (*go_forward_proto.NodeGetCapabilitiesResponse, error) {
	return s.node.NodeGetCapabilities(ctx, req)
}

func (s *proxyingServer) NodeGetInfo(ctx context.Context, req *go_forward_proto.NodeGetInfoRequest) (*go_forward_proto.NodeGetInfoResponse, error) {
	return s.node.NodeGetInfo(ctx, req)
}

// RegistrationService

func (s *proxyingServer) GetInfo(ctx context.Context, req *go_forward_proto.InfoRequest) (*go_forward_proto.PluginInfo, error) {
	return s.registration.GetInfo(ctx, req)
}

func (s *proxyingServer) NotifyRegistrationStatus(ctx context.Context, req *go_forward_proto.RegistrationStatus) (*go_forward_proto.RegistrationStatusResponse, error) {
	return s.registration.NotifyRegistrationStatus(ctx, req)
}

var portFlag = flag.Int("port", 20001, "the port of the upstream server")
var sockAddr = flag.String("unix", "", "the path to expose the unix socket at")

func main() {
	flag.Parse()

	fmt.Printf("port: %d\n", *portFlag)
	fmt.Printf("unix: %s\n", *sockAddr)

	conn, err := grpc.Dial(fmt.Sprintf("localhost:%d", *portFlag), grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		log.Fatal(err)
	}
	defer conn.Close()

	proxy := proxyingServer{
		node:         go_forward_proto.NewNodeClient(conn),
		identity:     go_forward_proto.NewIdentityClient(conn),
		registration: go_forward_proto.NewRegistrationClient(conn),
	}

	if _, err := os.Stat(*sockAddr); !os.IsNotExist(err) {
		if err := os.RemoveAll(*sockAddr); err != nil {
			log.Fatal(err)
		}
	}

	lis, _ := net.Listen("unix", *sockAddr)

	fmt.Printf("starting grpc forwarding server...\n")

	grpcServer := grpc.NewServer()
	go_forward_proto.RegisterIdentityServer(grpcServer, &proxy)
	go_forward_proto.RegisterNodeServer(grpcServer, &proxy)
	go_forward_proto.RegisterRegistrationServer(grpcServer, &proxy)
	grpcServer.Serve(lis)
}
