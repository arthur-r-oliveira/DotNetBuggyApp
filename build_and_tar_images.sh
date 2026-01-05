#!/bin/bash

# Define image names and their corresponding Containerfiles
# Format: "<full_image_tag>,<Containerfile_name>"
IMAGES=(
    "quay.io/rhn_support_arolivei/dotnet-memory-leak-app:v1-base,Containerfile"
    "quay.io/rhn_support_arolivei/dotnet-memory-leak-app:v1-debug,Containerfile-debug"
    "quay.io/rhn_support_arolivei/dotnet-memory-leak-app:v1-debug-shellless,Containerfile-debug-shellless"
    "quay.io/rhn_support_arolivei/dotnet-memory-leak-app:v1-hardened,Containerfile.hardened"
)

# Build and save each image
for IMAGE_INFO in "${IMAGES[@]}"; do
    IFS=',' read -r IMAGE_TAG CONTAINERFILE <<< "$IMAGE_INFO"
    
    echo "Building image: $IMAGE_TAG from $CONTAINERFILE"
    podman build -t "$IMAGE_TAG" -f "$CONTAINERFILE" .

    if [ $? -eq 0 ]; then
        # Replace problematic characters for filename: colons and slashes
        TAR_FILE=$(echo "$IMAGE_TAG" | sed 's/:/_/g' | sed 's/\//-/g').tar
        echo "Saving image $IMAGE_TAG to $TAR_FILE"
        podman save -o "$TAR_FILE" "$IMAGE_TAG"
        if [ $? -eq 0 ]; then
            echo "Successfully built and saved $IMAGE_TAG to $TAR_FILE"
        else
            echo "Error saving image $IMAGE_TAG"
        fi
    else
        echo "Error building image $IMAGE_TAG"
    fi
    echo "" # Add a newline for better readability
done

echo "All specified images have been processed."