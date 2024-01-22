using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Storage;

namespace uart
{
    // using Windows.AI.MachineLearning;
    internal class GloveModel : IDisposable
    {
        // Info：模型输入帧数，用于错误判断
        public const ushort framesNum = 10;
        // Info：和数据集最大值挂钩，用于归一化，注意及时修改
        private const float _max = 3533;
        private readonly string[] _labels = ["cube-gum", "cube-sponge", "cylinder-gum", "cylinder-sponge", "empty"];

        private LearningModel _model;
        private LearningModelSession _session;
        
        public async Task initialize()
        {
            try {
                // Load and create the model and session
                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets//R3d18.onnx"));
                _model = await LearningModel.LoadFromStorageFileAsync(modelFile);
                //支持的 ONNX opset 最大为15
                _session = new LearningModelSession(_model);
            } catch (Exception e) {
                Debug.WriteLine(e);
                _model = null;
            }
        }

        private static float Sigmoid(float value)
        {
            var k = (float)Math.Exp(value);
            return k / (1.0f + k);
        }
        private static float[] Softmax(float[] values)
        {
            var maxVal = values.Max();
            var exp = values.Select(v => Math.Exp(v - maxVal));
            var sumExp = exp.Sum();
            return exp.Select(v => (float)(v / sumExp)).ToArray();
        }

        private TensorFloat dataPreprocess(ushort[][,] frames)
        {
            long[] shape = [1, frames.GetLength(0), frames[0].GetLength(0), frames[0].GetLength(1)];
            float[] tmp = new float[shape[1] * shape[2] * shape[3]];
            for (var i = 0; i < shape[1]; i++)
                for (var j = 0; j < shape[2]; j++)
                    for (var k = 0; k < shape[3]; k++)
                        tmp[i * shape[3] * shape[2] + j * shape[3] + k] = frames[i][j, k] / _max;
            TensorFloat tensors = TensorFloat.CreateFromArray(shape, tmp);
            return tensors;
        }

        internal (string, float) evaluate(ushort[][,] frames)
        {
            if (frames.GetLength(0) != framesNum)
                throw new Exception("帧数与模型输入不对应");
            TensorFloat tensor = dataPreprocess(frames);
            // bind the tensor to "input"
            var binding = new LearningModelBinding(_session);
            binding.Bind("input", tensor);
            // evaluate
            var results = _session.Evaluate(binding, "output");
            // get the results
            TensorFloat prediction = (TensorFloat)results.Outputs.First().Value;
            var prediction_data = prediction.GetAsVectorView().ToArray();
            prediction_data = Softmax(prediction_data);
            // find the highest predicted value
            int max_index = 0;
            float max_value = 0;
            for (int i = 0; i < prediction_data.Length; i++)
                if (prediction_data[i] > max_value)
                {
                    max_value = prediction_data[i];
                    max_index = i;
                }
            // return the label corresponding to the highest predicted value
            return (_labels[max_index], max_value);
        }

        public void Dispose()
        {
            _model.Dispose();
            _model = null;
            _session.Dispose();
            _session = null;
        }
    }
}
