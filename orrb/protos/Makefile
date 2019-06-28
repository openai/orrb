PROTOC = protoc
GRPC_PYTHON_PLUGIN = grpc_python_plugin
GRPC_CSHARP_PLUGIN = grpc_csharp_plugin
GRPC_PYTHON_PLUGIN_PATH ?= `which $(GRPC_PYTHON_PLUGIN)`
GRPC_CSHARP_PLUGIN_PATH ?= `which $(GRPC_CSHARP_PLUGIN)`

PY_DIR = orrb/protos
CSHARP_DIR = unity/Assets/Scripts/Generated

all: RendererConfig_pb2.py RenderService_pb2.py RenderService_pb2_grpc.py

%_pb2.py: %.proto
	cd ../.. ; $(PROTOC) --proto_path . -I . --python_out . $(PY_DIR)/$<
	cd ../.. ; $(PROTOC) --proto_path . -I . --csharp_out $(CSHARP_DIR) $(PY_DIR)/$<

%_pb2_grpc.py: %.proto
	cd ../.. ; $(PROTOC) --proto_path . -I . --grpc_out . --plugin=protoc-gen-grpc=$(GRPC_PYTHON_PLUGIN_PATH) $(PY_DIR)/$<
	cd ../.. ; $(PROTOC) --proto_path . -I . --grpc_out $(CSHARP_DIR) --plugin=protoc-gen-grpc=$(GRPC_CSHARP_PLUGIN_PATH) $(PY_DIR)/$<


clean:
	rm -f *_pb2.py *_pb2_grpc.py ../../$(CSHARP_DIR)/*.cs
