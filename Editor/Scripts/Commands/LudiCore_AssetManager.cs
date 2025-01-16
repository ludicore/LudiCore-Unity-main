using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace IndieBuff.Editor
{
    public class AssetManager : ICommandManager
    {
        public static string CreateMaterial(Dictionary<string, string> parameters)
        {
            string materialName = parameters.ContainsKey("material_name") ? parameters["material_name"] : null;
            string colorName = parameters.ContainsKey("color") ? parameters["color"] : null;

            if (string.IsNullOrEmpty(materialName))
            {
                return "Failed to create material: Material name is required";
            }

            // Create the Materials folder if it doesn't exist
            string folderPath = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            // Create material
            Material material = new Material(Shader.Find("Standard"));
            
            // Set color if provided
            if (!string.IsNullOrEmpty(colorName))
            {
                bool colorSet = false;
                
                // RGBA vals
                string[] rgbaValues = colorName.Split(',');
                if (rgbaValues.Length >= 3) // Allow both RGB and RGBA
                {
                    if (float.TryParse(rgbaValues[0].Trim(), out float r) &&
                        float.TryParse(rgbaValues[1].Trim(), out float g) &&
                        float.TryParse(rgbaValues[2].Trim(), out float b))
                    {
                        // Default alpha to 1 if not provided
                        float a = rgbaValues.Length >= 4 && float.TryParse(rgbaValues[3].Trim(), out float alpha) ? alpha : 1f;
                        material.color = new Color(r, g, b, a);
                        colorSet = true;
                    }
                }

                // hex vals
                if (!colorSet && ColorUtility.TryParseHtmlString(colorName, out Color color))
                {
                    material.color = color;
                    colorSet = true;
                }

                // name vals (red etc)
                if (!colorSet)
                {
                    System.Type colorType = typeof(Color);
                    var colorProperty = colorType.GetProperty(colorName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (colorProperty != null)
                    {
                        material.color = (Color)colorProperty.GetValue(null);
                    }
                }
            }

            if (!materialName.EndsWith(".mat"))
                materialName += ".mat";

            string assetPath = Path.Combine(folderPath, materialName);
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Material created successfully at: {assetPath}";
        }

        public static string CreateTexture(Dictionary<string, string> parameters)
        {
            string textureName = parameters.ContainsKey("texture_name") ? parameters["texture_name"] : null;
            string colorName = parameters.ContainsKey("color") ? parameters["color"] : null;
            string widthStr = parameters.ContainsKey("width") ? parameters["width"] : "256";
            string heightStr = parameters.ContainsKey("height") ? parameters["height"] : "256";

            if (string.IsNullOrEmpty(textureName))
            {
                return "Failed to create texture: Texture name is required";
            }

            // Parse dimensions with defaults
            if (!int.TryParse(widthStr, out int width)) width = 256;
            if (!int.TryParse(heightStr, out int height)) height = 256;

            // Create the Textures folder if it doesn't exist
            string folderPath = "Assets/Textures";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Textures");
            }

            // Create texture
            Texture2D texture = new Texture2D(width, height);
            Color fillColor = Color.white; // Default color

            // Set color if provided
            if (!string.IsNullOrEmpty(colorName))
            {
                // RGBA vals
                string[] rgbaValues = colorName.Split(',');
                if (rgbaValues.Length >= 3)
                {
                    if (float.TryParse(rgbaValues[0].Trim(), out float r) &&
                        float.TryParse(rgbaValues[1].Trim(), out float g) &&
                        float.TryParse(rgbaValues[2].Trim(), out float b))
                    {
                        float a = rgbaValues.Length >= 4 && float.TryParse(rgbaValues[3].Trim(), out float alpha) ? alpha : 1f;
                        fillColor = new Color(r, g, b, a);
                    }
                }
                // hex vals
                else if (ColorUtility.TryParseHtmlString(colorName, out Color color))
                {
                    fillColor = color;
                }
                // name vals (red etc)
                else
                {
                    System.Type colorType = typeof(Color);
                    var colorProperty = colorType.GetProperty(colorName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (colorProperty != null)
                    {
                        fillColor = (Color)colorProperty.GetValue(null);
                    }
                }
            }

            // Fill texture with color
            Color[] colors = new Color[width * height];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = fillColor;
            }
            texture.SetPixels(colors);
            texture.Apply();

            // Save texture
            if (!textureName.EndsWith(".png"))
                textureName += ".png";

            string assetPath = Path.Combine(folderPath, textureName);
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(assetPath, bytes);

            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Texture created successfully at: {assetPath}";
        }

        public static string RenameAsset(Dictionary<string, string> parameters)
        {
            string assetPath = parameters.ContainsKey("asset_path") ? parameters["asset_path"] : null;
            string newName = parameters.ContainsKey("asset_name") ? parameters["asset_name"] : null;

            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(newName))
            {
                return "Failed to rename asset: Both asset path and new name are required";
            }

            // Ensure the asset path starts with "Assets/"
            if (!assetPath.StartsWith("Assets/"))
            {
                assetPath = "Assets/" + assetPath;
            }

            if (!File.Exists(assetPath))
            {
                return $"Failed to rename asset: Asset not found at path {assetPath}";
            }

            // Get directory and extension
            string directory = Path.GetDirectoryName(assetPath);
            string extension = Path.GetExtension(assetPath);

            // Ensure new name has the correct extension
            if (!newName.EndsWith(extension))
            {
                newName += extension;
            }

            string newPath = Path.Combine(directory, newName);

            // Check if target name already exists
            if (File.Exists(newPath))
            {
                return $"Failed to rename asset: An asset with name {newName} already exists";
            }

            AssetDatabase.RenameAsset(assetPath, Path.GetFileNameWithoutExtension(newName));
            AssetDatabase.SaveAssets();

            return $"Asset renamed successfully to: {newName}";
        }

        public static string DuplicateAsset(Dictionary<string, string> parameters)
        {
            string assetPath = parameters.ContainsKey("asset_path") ? parameters["asset_path"] : null;
            string newName = parameters.ContainsKey("asset_name") ? parameters["asset_name"] : null;

            if (string.IsNullOrEmpty(assetPath))
            {
                return "Failed to duplicate asset: Asset path is required";
            }

            if (!assetPath.StartsWith("Assets/"))
            {
                assetPath = "Assets/" + assetPath;
            }

            if (!File.Exists(assetPath))
            {
                return $"Failed to duplicate asset: Asset not found at path {assetPath}";
            }

            string directory = Path.GetDirectoryName(assetPath);
            string newPath;

            string fileName = Path.GetFileName(assetPath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);


            if (string.IsNullOrEmpty(newName) || newName == fileNameWithoutExtension)
            {
                newPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            }
            else
            {
                string extension = Path.GetExtension(assetPath);
                if (!newName.EndsWith(extension))
                    newName += extension;
                newPath = Path.Combine(directory, newName);
            }

            AssetDatabase.CopyAsset(assetPath, newPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Asset duplicated successfully to: {newPath}";
        }



        public static string DeleteAsset(Dictionary<string, string> parameters)
        {
            string assetPath = parameters.ContainsKey("asset_path") ? parameters["asset_path"] : null;

            if (string.IsNullOrEmpty(assetPath))
            {
                return "Failed to delete asset: Asset path is required";
            }

            if (!assetPath.StartsWith("Assets/"))
            {
                assetPath = "Assets/" + assetPath;
            }

            if (!File.Exists(assetPath))
            {
                return $"Failed to delete asset: Asset not found at path {assetPath}";
            }


            bool success = AssetDatabase.MoveAssetToTrash(assetPath);
            
            if (success)
            {
                AssetDatabase.Refresh();
                return $"Asset successfully deleted at path: {assetPath}";
            }
            else
            {
                return $"Failed to delete asset at path: {assetPath}";
            }
        }
    }
} 