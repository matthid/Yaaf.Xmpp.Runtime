#!/bin/bash
echo "I use this script to release the current project to my other local projects, before actually releasing the nuget package"
echo "Use it at your own risk!"
echo "We assume the project folder is named to the nuget package and all projects are within the same folder!"

# we assume that we are in the project folder we want to release
for project_dir in src/source/*;
do
  project_name=${project_dir##*/}
  echo "Releasing project $project_name"

  for target_dir in ../*/packages/$project_name/lib/net45;
  do
    for ext in ".xml" ".dll" ".exe" ".pdb" ".mdb"; 
    do
      source=$project_dir/bin/Debug/${project_name}$ext
      dest=$target_dir/${project_name}$ext
      if [ -f $source ];
      then
        echo "Copying ${project_name}$ext to $dest"
        cp $source $dest
      fi
    done
  done
done
