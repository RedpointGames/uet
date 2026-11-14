import Foundation
import FSKit

@main
struct UefsVfsFileSystemExtension : UnaryFileSystemExtension {
	var fileSystem : FSUnaryFileSystem & FSUnaryFileSystemOperations {
		UefsVfsFileSystem()
	}
}