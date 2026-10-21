{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
    systems.url = "github:nix-systems/default";
    treefmt-nix = {
      url = "github:numtide/treefmt-nix";
      inputs.nixpkgs.follows = "nixpkgs";
    };
    flake-compat = {
      url = "github:NixOS/flake-compat";
      flake = false;
    };
  };

  outputs =
    {
      nixpkgs,
      ...
    }@inputs:
    with builtins;
    with nixpkgs.lib;

    let
      doubles = import inputs.systems;
      forAllSystems = genAttrs doubles;

      pkgs_gen =
        { ... }@args:
        import nixpkgs (
          {
            overlays = [ ];
          }
          // args
        );

      # The frontend is built with npm, the same package manager the project
      # uses everywhere else. `buildNpmPackage` consumes the committed
      # package-lock.json, so the dependency tree stays reproducible and the
      # build no longer depends on a bun.lock.
      frontend_src =
        pkgs.lib.cleanSourceWith {
          src = ./.;
          filter = path: type:
            let
              base = baseNameOf (toString path);
            in
            !(
              base == "src-tauri"
              || base == ".github"
              || base == ".git"
              || base == "node_modules"
              || base == "dist"
            );
        };

      fhzipa_inject_gen =
        {
          pkgs,
          tools ? [ ],
          specialArgs ? { },
        }:
        pkgs.rustPlatform.buildRustCrate (
          let
            src = cleanSource ./.;
            json = fromJSON (readFile (src + "/package.json"));

            frontend = pkgs.buildNpmPackage {
              pname = json.name + "-frontend";
              inherit (json) version;
              src = frontend_src;
              npmLockFile = frontend_src + "/package-lock.json";
              npmBuildScript = "build";
              dontFixup = true;
            };
          in
          {
            pname = json.name;
            inherit (json) version;

            inherit src;
            cargoRoot = "src-tauri";
            cargoLock.lockFile = src + "/${final.cargoRoot}/Cargo.lock";
            buildAndTestSubdir = final.cargoRoot;

            nativeBuildInputs =
              with pkgs; [
                cargo-tauri.hook
                nodejs
                npm
                pkg-config
                jq
                moreutils
              ]
              ++ optionals pkgs.stdenv.hostPlatform.isLinux [ wrapGAppsHook4 ]
              ++ tools;

            buildInputs = optionals pkgs.stdenv.hostPlatform.isLinux (
              with pkgs; [
                glib-networking
                openssl
                webkitgtk_4_1
              ]
            );

            # The node modules come from the npm derivation above, so the
            # Tauri hook can run "npm run build" without network access.
            postPatch = ''
              ln -s ${frontend.nodeModules} node_modules
              mkdir -p dist
              cp -r ${frontend}/dist/* dist/
            '';

            postInstall = ''
              jq \
                '.plugins.updater.endpoints = [ ]
                | .bundle.createUpdaterArtifacts = false' \
                ${final.cargoRoot}/tauri.conf.json \
                | sponge ${final.cargoRoot}/tauri.conf.json
            '';
          }
          // specialArgs
        );
    in
    {
      packages = forAllSystems (
        system:
        let
          pkgs = pkgs_gen { inherit system; };
          fhzipa_inject = fhzipa_inject_gen { inherit pkgs; };
        in
        {
          inherit fhzipa_inject;
          default = fhzipa_inject;
        }
      );
      formatter = forAllSystems (
        system:
        let
          pkgs = pkgs_gen { inherit system; };
        in
        inputs.treefmt-nix.lib.mkWrapper pkgs {
          programs.nixfmt.enable = true;
        }
      );
    };
}
