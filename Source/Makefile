.PHONY: all build clean distclean

all: build

build:
	dotnet publish BedOwnershipTools.slnx -c Release

clean:
	rm -rf .vs Artifacts

distclean:
	git clean -fdX :/
