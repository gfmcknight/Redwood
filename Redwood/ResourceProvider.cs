using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Redwood
{
    public interface IResourceProvider
    {
        bool TryGetResource(string resourceName, out Stream stream);
    }

    public class DirectoryResourceProvider : IResourceProvider
    {
        private string directory;
        
        public DirectoryResourceProvider(string directory)
        {
            this.directory = directory;
        }

        public bool TryGetResource(string resourceName, out Stream stream)
        {
            string resourcePath = Path.Combine(
                directory,
                resourceName.Replace(".", Path.DirectorySeparatorChar.ToString()) + ".rwd"
            );
            
            if (!File.Exists(resourcePath))
            {
                stream = null;
                return false;
            }
            stream = File.OpenRead(resourcePath);
            return true;
        }
    }

    public class FileResourceProvider : IResourceProvider
    {
        private string resource;
        private string filename;

        public FileResourceProvider(string resource, string filename)
        {
            this.resource = resource;
            this.filename = filename;

            if (!File.Exists(filename))
            {
                throw new FileNotFoundException(filename);
            }
        }

        public bool TryGetResource(string resourceName, out Stream stream)
        {
            if (resource != resourceName)
            {
                stream = null;
                return false;
            }

            stream = File.OpenRead(filename);
            return true;
        }
    }
}
