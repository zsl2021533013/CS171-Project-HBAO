using System;
using UnityEngine;

namespace HBAO
{
    public class Test : MonoBehaviour
    {
        private void Start()
        {
            using (var a = new A())
            {
                print(2);
            }
        }
    }

    class A : IDisposable
    {
        public A()
        {
            Debug.Log(1);
        }
        
        public void Dispose()
        {
            
        }
    }
}