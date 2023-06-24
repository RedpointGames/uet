param()

$global:ErrorActionPreference = "Stop"

# go install google.golang.org/grpc/cmd/protoc-gen-go-grpc@latest

Push-Location $PSScriptRoot
protoc --go_out=. --go_opt=paths=source_relative --go-grpc_out=. --go-grpc_opt=paths=source_relative proto/CSI.proto proto/PluginRegistration.proto