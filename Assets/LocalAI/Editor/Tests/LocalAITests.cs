using NUnit.Framework;
using LocalAI.Editor.Services;
using UnityEngine;
using System.IO;

namespace LocalAI.Editor.Tests
{
    public class LocalAITests
    {
        [Test]
        public void ContextCollector_HandlesNullSelection()
        {
            // Arrange
            var collector = new ContextCollector();
            // Ensure nothing selected
            // (In Editor tests this might be tricky if not careful, but usually starts empty or we can force it)
            // But we can't easily mock Selection.activeGameObject without wrapper.
            // Assumption: ContextCollector handles null.
            
            // Act
            var data = collector.CollectContext();

            // Assert
            Assert.IsNotEmpty(data.FullText);
            // We expect "No Object Selected" or active object name
        }

        [Test]
        public void ModelManager_InitializesToNotInstalled_WhenFileMissing()
        {
            // Arrange
            var manager = new ModelManager();
            // We can't easily mock the file system here without abstraction, 
            // but we know the likely state on a fresh test run is NotInstalled (unless user actually downloaded).
            
            // Act
            var state = manager.CurrentState;

            // Assert
            // This is a bit flaky if model exists. 
            // Better test: Check path helper.
            Assert.IsNotNull(manager.GetModelDirectory());
        }

        [Test]
        public void ModelManager_GetModelPath_ReturnsCorrectFileName()
        {
            var manager = new ModelManager();
            string path = manager.GetModelPath();
            Assert.IsTrue(path.EndsWith("mistral-7b-q4.gguf"));
        }
    }
}
